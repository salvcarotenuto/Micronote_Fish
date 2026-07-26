using MySqlConnector;

var connectionString = Environment.GetEnvironmentVariable("MICRONOTE_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione MySQL mancante.");

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

await using var command = new MySqlCommand(
    """
    SELECT COLUMN_NAME, DATA_TYPE
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND LOWER(TABLE_NAME) = 'movcontdc'
    ORDER BY ORDINAL_POSITION;
    """,
    connection);

await using var reader = await command.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    Console.WriteLine($"{reader.GetString("COLUMN_NAME")}\t{reader.GetString("DATA_TYPE")}");
}
