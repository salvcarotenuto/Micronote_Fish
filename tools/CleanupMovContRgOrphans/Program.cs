using MySqlConnector;

var connectionString = Environment.GetEnvironmentVariable("MICRONOTE_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione MySQL mancante.");

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

static async Task<long> ScalarAsync(MySqlConnection connection, MySqlTransaction? transaction, string sql)
{
    await using var command = new MySqlCommand(sql, connection, transaction);
    var value = await command.ExecuteScalarAsync();
    return Convert.ToInt64(value);
}

static async Task<int> ExecuteAsync(MySqlConnection connection, MySqlTransaction transaction, string sql)
{
    await using var command = new MySqlCommand(sql, connection, transaction);
    return await command.ExecuteNonQueryAsync();
}

const string orphanCountSql = """
    SELECT COUNT(*)
    FROM movcontrg r
    LEFT JOIN movcont m
      ON m.Anno = r.Anno
     AND m.Settore = r.Settore
     AND m.Codice = r.Codice
    WHERE (r.ID IS NULL OR r.ID = 0)
      AND m.ID IS NULL;
    """;

Console.WriteLine("Prima:");
Console.WriteLine($"  MovContRg: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM movcontrg;")}");
Console.WriteLine($"  Righe orfane con ID nullo/zero: {await ScalarAsync(connection, null, orphanCountSql)}");

await using var transaction = await connection.BeginTransactionAsync();
try
{
    var deleted = await ExecuteAsync(
        connection,
        transaction,
        """
        DELETE r
        FROM movcontrg r
        LEFT JOIN movcont m
          ON m.Anno = r.Anno
         AND m.Settore = r.Settore
         AND m.Codice = r.Codice
        WHERE (r.ID IS NULL OR r.ID = 0)
          AND m.ID IS NULL;
        """);

    await transaction.CommitAsync();
    Console.WriteLine($"Eliminate: {deleted}");
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

Console.WriteLine("Dopo:");
Console.WriteLine($"  MovContRg: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM movcontrg;")}");
Console.WriteLine($"  Righe orfane con ID nullo/zero: {await ScalarAsync(connection, null, orphanCountSql)}");

return 0;
