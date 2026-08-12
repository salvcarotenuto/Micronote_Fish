using MySqlConnector;

if (args.Length != 1)
{
    Console.Error.WriteLine("Uso: AlignArticleFishSchema <database-mysql>");
    return 2;
}

var connectionString = Environment.GetEnvironmentVariable("MICRONOTE_DB_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Variabile MICRONOTE_DB_CONNECTION non configurata.");
    return 3;
}

var builder = new MySqlConnectionStringBuilder(connectionString)
{
    Database = args[0]
};

await using var connection = new MySqlConnection(builder.ConnectionString);
await connection.OpenAsync();

await using (var countCommand = new MySqlCommand(
    "SELECT COUNT(*) FROM `Articoli`;",
    connection))
{
    var rowCount = Convert.ToInt64(await countCommand.ExecuteScalarAsync());
    if (rowCount != 0)
    {
        Console.Error.WriteLine(
            $"Operazione interrotta: Articoli contiene {rowCount} record.");
        return 4;
    }
}

foreach (var (column, definition) in new[]
{
    ("Umv", "VARCHAR(20) NULL DEFAULT ''"),
    ("GiacinC", "INT NULL DEFAULT 0")
})
{
    await using var modifyCommand = new MySqlCommand(
        $"ALTER TABLE `Articoli` MODIFY COLUMN `{column}` {definition};",
        connection);
    await modifyCommand.ExecuteNonQueryAsync();
    Console.WriteLine($"{column}: adeguata a {definition}");
}

foreach (var column in new[] { "Sottogruppo", "Pezzi", "GiacIn", "Consumo", "PesoNt" })
{
    await using var existsCommand = new MySqlCommand(
        """
        SELECT COUNT(*)
        FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND LOWER(TABLE_NAME) = 'articoli'
          AND LOWER(COLUMN_NAME) = LOWER(@column);
        """,
        connection);
    existsCommand.Parameters.AddWithValue("@column", column);

    if (Convert.ToInt32(await existsCommand.ExecuteScalarAsync()) == 0)
    {
        Console.WriteLine($"{column}: già assente");
        continue;
    }

    await using var dropCommand = new MySqlCommand(
        $"ALTER TABLE `Articoli` DROP COLUMN `{column}`;",
        connection);
    await dropCommand.ExecuteNonQueryAsync();
    Console.WriteLine($"{column}: eliminata");
}

return 0;
