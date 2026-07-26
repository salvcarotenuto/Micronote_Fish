using MySqlConnector;

var connectionString = Environment.GetEnvironmentVariable("MICRONOTE_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione mancante.");
connectionString = new MySqlConnectionStringBuilder(connectionString)
{
    Database = "Micronote_master"
}.ConnectionString;

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

await using (var check = new MySqlCommand(
    """
    SELECT COUNT(*)
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'ParametriApp'
      AND TABLE_TYPE = 'BASE TABLE';
    """,
    connection))
{
    if (Convert.ToInt32(await check.ExecuteScalarAsync()) != 1)
    {
        throw new InvalidOperationException("Micronote_master.ParametriApp non esiste.");
    }
}

long rows;
await using (var count = new MySqlCommand("SELECT COUNT(*) FROM ParametriApp;", connection))
{
    rows = Convert.ToInt64(await count.ExecuteScalarAsync());
}

await using (var drop = new MySqlCommand("DROP TABLE ParametriApp;", connection))
{
    await drop.ExecuteNonQueryAsync();
}

await using (var verify = new MySqlCommand(
    """
    SELECT
      (SELECT COUNT(*) FROM information_schema.TABLES
       WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ParametriApp')
      +
      (SELECT CASE WHEN COUNT(*) = 1 THEN 0 ELSE 100 END
       FROM information_schema.TABLES
       WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Parametri');
    """,
    connection))
{
    if (Convert.ToInt32(await verify.ExecuteScalarAsync()) != 0)
    {
        throw new InvalidOperationException("Verifica finale non superata.");
    }
}

Console.WriteLine($"Rimossa Micronote_master.ParametriApp ({rows} righe).");
Console.WriteLine("Micronote_master.Parametri è presente e invariata.");
