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

static async Task PrintStatsAsync(MySqlConnection connection, string title)
{
    Console.WriteLine(title);
    Console.WriteLine($"  MovCont: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM movcont;")}");
    Console.WriteLine($"  MovContRg: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM movcontrg;")}");
    Console.WriteLine($"  MovCont senza righe figlie: {await ScalarAsync(connection, null, """
        SELECT COUNT(*)
        FROM movcont m
        LEFT JOIN movcontrg r ON r.ID = m.ID
        WHERE r.ID IS NULL;
        """)}");
    Console.WriteLine($"  Chiavi MovCont duplicate Anno+Settore+Codice: {await ScalarAsync(connection, null, """
        SELECT COUNT(*)
        FROM (
            SELECT Anno, Settore, Codice
            FROM movcont
            GROUP BY Anno, Settore, Codice
            HAVING COUNT(*) > 1
        ) d;
        """)}");
}

await PrintStatsAsync(connection, "Prima:");

await using var transaction = await connection.BeginTransactionAsync();
try
{
    var deleted = await ExecuteAsync(
        connection,
        transaction,
        """
        DELETE m
        FROM movcont m
        LEFT JOIN movcontrg r ON r.ID = m.ID
        WHERE r.ID IS NULL;
        """);

    await transaction.CommitAsync();
    Console.WriteLine($"Eliminate: {deleted}");
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

await PrintStatsAsync(connection, "Dopo:");

return 0;
