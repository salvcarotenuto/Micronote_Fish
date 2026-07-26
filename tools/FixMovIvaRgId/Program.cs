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

var hasId = await ScalarAsync(
    connection,
    null,
    """
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'movivarg'
      AND COLUMN_NAME = 'ID';
    """);

if (hasId == 0)
{
    Console.WriteLine("ERRORE: la colonna ID non esiste in movivarg.");
    Environment.ExitCode = 2;
    return;
}

Console.WriteLine("Prima:");
Console.WriteLine($"  MovIva: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM moviva;")}");
Console.WriteLine($"  MovIvaRg: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM movivarg;")}");
Console.WriteLine($"  MovIvaRg con ID nullo: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM movivarg WHERE ID IS NULL;")}");
Console.WriteLine($"  MovIvaRg senza madre: {await ScalarAsync(connection, null, """
    SELECT COUNT(*)
    FROM movivarg r
    LEFT JOIN moviva m
      ON m.Anno = r.Anno
     AND m.Settore = r.Settore
     AND m.Codice = r.Codice
    WHERE m.ID IS NULL;
    """)}");
Console.WriteLine($"  Chiavi madri duplicate: {await ScalarAsync(connection, null, """
    SELECT COUNT(*)
    FROM (
        SELECT Anno, Settore, Codice
        FROM moviva
        GROUP BY Anno, Settore, Codice
        HAVING COUNT(*) > 1
    ) d;
    """)}");

await using var transaction = await connection.BeginTransactionAsync();
try
{
    var updated = await ExecuteAsync(
        connection,
        transaction,
        """
        UPDATE movivarg r
        INNER JOIN moviva m
           ON m.Anno = r.Anno
          AND m.Settore = r.Settore
          AND m.Codice = r.Codice
        SET r.ID = m.ID
        WHERE r.ID IS NULL OR r.ID <> m.ID;
        """);

    await transaction.CommitAsync();
    Console.WriteLine($"Aggiornate: {updated}");
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

Console.WriteLine("Dopo:");
Console.WriteLine($"  MovIvaRg con ID nullo: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM movivarg WHERE ID IS NULL;")}");
Console.WriteLine($"  Join MovIva/MovIvaRg via ID: {await ScalarAsync(connection, null, """
    SELECT COUNT(*)
    FROM movivarg r
    INNER JOIN moviva m ON m.ID = r.ID;
    """)}");
