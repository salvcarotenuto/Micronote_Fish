using MySqlConnector;

if (args.Length != 1)
{
    Console.Error.WriteLine("Uso: AlignArticleDecimalColumns <database-mysql>");
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

foreach (var column in new[] { "CostoStd", "PrezzoStd" })
{
    await using var command = new MySqlCommand(
        $"ALTER TABLE `Articoli` MODIFY COLUMN `{column}` DECIMAL(10,3) NULL DEFAULT 0.000;",
        connection);
    await command.ExecuteNonQueryAsync();
    Console.WriteLine($"{column}: DECIMAL(10,3)");
}

return 0;
