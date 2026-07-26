using System.Text.RegularExpressions;
using MySqlConnector;

const string sourceDatabase = "Micronote_0001";
const string targetDatabase = "Micronote_master";
const string catalogTable = "FixedTable";

var connectionString = Environment.GetEnvironmentVariable("MICRONOTE_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione mancante.");
var apply = args.Any(arg => arg.Equals("--apply", StringComparison.OrdinalIgnoreCase));
var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

await RequireDatabaseAsync(connection, sourceDatabase);
await RequireDatabaseAsync(connection, targetDatabase);
await RequireTableAsync(connection, sourceDatabase, catalogTable);

var fixedTableNames = await ReadFixedTableNamesAsync(connection);
if (fixedTableNames.Count != 27)
{
    throw new InvalidOperationException(
        $"La tabella {sourceDatabase}.{catalogTable} contiene {fixedTableNames.Count} nomi distinti validi; attesi 27.");
}

foreach (var tableName in fixedTableNames)
{
    await RequireTableAsync(connection, sourceDatabase, tableName);
}

var tablesToCopy = new[] { catalogTable }
    .Concat(fixedTableNames)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

Console.WriteLine($"Sorgente: {sourceDatabase}");
Console.WriteLine($"Destinazione: {targetDatabase}");
Console.WriteLine($"Tabelle fisse elencate: {fixedTableNames.Count}");
Console.WriteLine($"Tabelle complessive da copiare: {tablesToCopy.Length}");
Console.WriteLine();

foreach (var tableName in tablesToCopy)
{
    var sourceRows = await CountRowsAsync(connection, sourceDatabase, tableName);
    var targetExists = await TableExistsAsync(connection, targetDatabase, tableName);
    var targetRows = targetExists
        ? await CountRowsAsync(connection, targetDatabase, tableName)
        : 0;
    var targetDisplay = targetExists ? targetRows.ToString() : "non presente";
    Console.WriteLine(
        $"{tableName}: sorgente={sourceRows}, master={targetDisplay}");
}

if (!apply)
{
    Console.WriteLine();
    Console.WriteLine("Ricognizione superata. Rieseguire con --apply per copiare struttura e dati.");
    return;
}

var copied = new List<string>();
try
{
    foreach (var tableName in tablesToCopy)
    {
        var sourceRows = await CountRowsAsync(connection, sourceDatabase, tableName);
        var targetExists = await TableExistsAsync(connection, targetDatabase, tableName);
        if (targetExists)
        {
            var backupName = BackupName(tableName, stamp);
            await ExecuteAsync(
                connection,
                $"CREATE TABLE {Q(targetDatabase)}.{Q(backupName)} LIKE {Q(targetDatabase)}.{Q(tableName)};");
            await ExecuteAsync(
                connection,
                $"INSERT INTO {Q(targetDatabase)}.{Q(backupName)} SELECT * FROM {Q(targetDatabase)}.{Q(tableName)};");
        }

        var stagingName = StagingName(tableName, stamp);
        await ExecuteAsync(
            connection,
            $"CREATE TABLE {Q(targetDatabase)}.{Q(stagingName)} LIKE {Q(sourceDatabase)}.{Q(tableName)};");
        await ExecuteAsync(
            connection,
            $"INSERT INTO {Q(targetDatabase)}.{Q(stagingName)} SELECT * FROM {Q(sourceDatabase)}.{Q(tableName)};");

        var stagingRows = await CountRowsAsync(connection, targetDatabase, stagingName);
        if (stagingRows != sourceRows)
        {
            throw new InvalidOperationException(
                $"Copia non coerente per {tableName}: sorgente={sourceRows}, temporanea={stagingRows}.");
        }

        if (targetExists)
        {
            await ExecuteAsync(connection, $"DROP TABLE {Q(targetDatabase)}.{Q(tableName)};");
        }
        await ExecuteAsync(
            connection,
            $"RENAME TABLE {Q(targetDatabase)}.{Q(stagingName)} TO {Q(targetDatabase)}.{Q(tableName)};");
        copied.Add(tableName);
    }
}
catch
{
    Console.Error.WriteLine($"Copia interrotta dopo {copied.Count} tabelle completate.");
    Console.Error.WriteLine($"Le copie di sicurezza hanno suffisso _PreFixedExport_{stamp}.");
    throw;
}

Console.WriteLine();
Console.WriteLine($"Esportazione completata: {copied.Count} tabelle copiate in {targetDatabase}.");
Console.WriteLine($"Backup delle tabelle preesistenti: suffisso _PreFixedExport_{stamp}.");

async Task<IReadOnlyList<string>> ReadFixedTableNamesAsync(MySqlConnection connection)
{
    await using var command = new MySqlCommand(
        $"SELECT * FROM {Q(sourceDatabase)}.{Q(catalogTable)};",
        connection);
    await using var reader = await command.ExecuteReaderAsync();
    var names = new List<string>();
    while (await reader.ReadAsync())
    {
        string? name = null;
        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (reader.IsDBNull(index))
            {
                continue;
            }

            var candidate = Convert.ToString(reader.GetValue(index))?.Trim();
            if (!string.IsNullOrWhiteSpace(candidate)
                && Regex.IsMatch(candidate, "^[A-Za-z0-9_]+$"))
            {
                name = candidate;
                break;
            }
        }

        if (name is not null
            && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            names.Add(name);
        }
    }

    return names;
}

static async Task RequireDatabaseAsync(MySqlConnection connection, string databaseName)
{
    await using var command = new MySqlCommand(
        "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = @name;",
        connection);
    command.Parameters.AddWithValue("@name", databaseName);
    if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
    {
        throw new InvalidOperationException($"Database non trovato: {databaseName}.");
    }
}

static async Task RequireTableAsync(
    MySqlConnection connection,
    string databaseName,
    string tableName)
{
    if (!await TableExistsAsync(connection, databaseName, tableName))
    {
        throw new InvalidOperationException($"Tabella non trovata: {databaseName}.{tableName}.");
    }
}

static async Task<bool> TableExistsAsync(
    MySqlConnection connection,
    string databaseName,
    string tableName)
{
    await using var command = new MySqlCommand(
        """
        SELECT COUNT(*)
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = @databaseName
          AND TABLE_NAME = @tableName
          AND TABLE_TYPE = 'BASE TABLE';
        """,
        connection);
    command.Parameters.AddWithValue("@databaseName", databaseName);
    command.Parameters.AddWithValue("@tableName", tableName);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
}

static async Task<long> CountRowsAsync(
    MySqlConnection connection,
    string databaseName,
    string tableName)
{
    await using var command = new MySqlCommand(
        $"SELECT COUNT(*) FROM {Q(databaseName)}.{Q(tableName)};",
        connection);
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}

static async Task ExecuteAsync(MySqlConnection connection, string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}

static string BackupName(string tableName, string stamp) =>
    $"{tableName}_PreFixedExport_{stamp}";

static string StagingName(string tableName, string stamp) =>
    $"{tableName}_FixedExportTmp_{stamp}";

static string Q(string identifier) => $"`{identifier.Replace("`", "``")}`";
