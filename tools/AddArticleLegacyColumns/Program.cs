using MySqlConnector;

if (args.Length != 1)
{
    Console.Error.WriteLine("Uso: AddArticleLegacyColumns <database-mysql>");
    return 2;
}

var connectionString = Environment.GetEnvironmentVariable("MICRONOTE_DB_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Variabile MICRONOTE_DB_CONNECTION non configurata.");
    return 3;
}

var columns = new (string Name, string Definition)[]
{
    ("Specie", "SMALLINT NULL DEFAULT 0"),
    ("Umv", "VARCHAR(10) NULL DEFAULT ''"),
    ("Provenienza", "SMALLINT NULL DEFAULT 0"),
    ("Tara", "DECIMAL(10,3) NULL DEFAULT 0.000"),
    ("GiacinC", "DECIMAL(10,3) NULL DEFAULT 0.000"),
    ("GiacinP", "DECIMAL(10,3) NULL DEFAULT 0.000"),
    ("PrIvato", "DECIMAL(10,3) NULL DEFAULT 0.000")
};

var builder = new MySqlConnectionStringBuilder(connectionString)
{
    Database = args[0]
};

await using var connection = new MySqlConnection(builder.ConnectionString);
await connection.OpenAsync();

foreach (var (name, definition) in columns)
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
    existsCommand.Parameters.AddWithValue("@column", name);
    var exists = Convert.ToInt32(await existsCommand.ExecuteScalarAsync()) > 0;
    if (exists)
    {
        Console.WriteLine($"{name}: già presente");
        continue;
    }

    await using var alterCommand = new MySqlCommand(
        $"ALTER TABLE `Articoli` ADD COLUMN `{name}` {definition};",
        connection);
    await alterCommand.ExecuteNonQueryAsync();
    Console.WriteLine($"{name}: aggiunta");
}

return 0;
