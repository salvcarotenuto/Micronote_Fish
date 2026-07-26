using MySqlConnector;

var localConnectionString = Environment.GetEnvironmentVariable("MICRONOTE_LOCAL_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione locale mancante.");
var remoteConnectionString = Environment.GetEnvironmentVariable("MICRONOTE_REMOTE_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione remota mancante.");

localConnectionString = new MySqlConnectionStringBuilder(localConnectionString)
{
    Database = "Micronote_master"
}.ConnectionString;

await using var local = new MySqlConnection(localConnectionString);
await using var remote = new MySqlConnection(remoteConnectionString);
await local.OpenAsync();
await remote.OpenAsync();

foreach (var tableName in new[] { "Accessi", "FixedTable" })
{
    if (await TableExistsAsync(remote, tableName))
    {
        throw new InvalidOperationException(
            $"{remote.Database}.{tableName} esiste già: nessuna modifica eseguita.");
    }
}

var created = new List<string>();
try
{
    foreach (var tableName in new[] { "Accessi", "FixedTable" })
    {
        var createSql = await ReadCreateTableAsync(local, tableName);
        await ExecuteAsync(remote, createSql);
        created.Add(tableName);
    }

    await CopyRowsAsync(local, remote, "FixedTable");

    var accessRows = await CountRowsAsync(remote, "Accessi");
    var localFixedRows = await CountRowsAsync(local, "FixedTable");
    var remoteFixedRows = await CountRowsAsync(remote, "FixedTable");
    if (accessRows != 0 || localFixedRows != remoteFixedRows)
    {
        throw new InvalidOperationException(
            $"Verifica dati non superata: Accessi={accessRows}, FixedTable locale={localFixedRows}, remoto={remoteFixedRows}.");
    }
}
catch
{
    foreach (var tableName in created.AsEnumerable().Reverse())
    {
        if (await TableExistsAsync(remote, tableName))
        {
            await ExecuteAsync(remote, $"DROP TABLE {Q(tableName)};");
        }
    }
    throw;
}

Console.WriteLine($"Creata {remote.Database}.Accessi: struttura, 0 righe.");
Console.WriteLine($"Creata {remote.Database}.FixedTable: struttura e {await CountRowsAsync(remote, "FixedTable")} righe.");

static async Task CopyRowsAsync(
    MySqlConnection source,
    MySqlConnection target,
    string tableName)
{
    var rows = new List<object?[]>();
    string[] columns;
    await using (var read = new MySqlCommand($"SELECT * FROM {Q(tableName)};", source))
    await using (var reader = await read.ExecuteReaderAsync())
    {
        columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();
        while (await reader.ReadAsync())
        {
            var values = new object?[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(values);
        }
    }

    var columnSql = string.Join(", ", columns.Select(Q));
    var parameterSql = string.Join(", ", columns.Select((_, index) => $"@p{index}"));
    foreach (var values in rows)
    {
        await using var insert = new MySqlCommand(
            $"INSERT INTO {Q(tableName)} ({columnSql}) VALUES ({parameterSql});",
            target);
        for (var index = 0; index < values.Length; index++)
        {
            insert.Parameters.AddWithValue($"@p{index}", values[index] ?? DBNull.Value);
        }
        await insert.ExecuteNonQueryAsync();
    }
}

static async Task<string> ReadCreateTableAsync(MySqlConnection connection, string tableName)
{
    await using var command = new MySqlCommand($"SHOW CREATE TABLE {Q(tableName)};", connection);
    await using var reader = await command.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        throw new InvalidOperationException($"Definizione locale non trovata: {tableName}.");
    }
    return reader.GetString(1);
}

static async Task<bool> TableExistsAsync(MySqlConnection connection, string tableName)
{
    await using var command = new MySqlCommand(
        """
        SELECT COUNT(*)
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @tableName
          AND TABLE_TYPE = 'BASE TABLE';
        """,
        connection);
    command.Parameters.AddWithValue("@tableName", tableName);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
}

static async Task<long> CountRowsAsync(MySqlConnection connection, string tableName)
{
    await using var command = new MySqlCommand($"SELECT COUNT(*) FROM {Q(tableName)};", connection);
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}

static async Task ExecuteAsync(MySqlConnection connection, string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}

static string Q(string identifier) => $"`{identifier.Replace("`", "``")}`";
