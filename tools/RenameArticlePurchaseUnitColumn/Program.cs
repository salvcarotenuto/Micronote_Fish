using MySqlConnector;

if (args.Length != 1)
{
    Console.Error.WriteLine("Uso: RenameArticlePurchaseUnitColumn <database-mysql>");
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

static async Task<bool> ColumnExistsAsync(MySqlConnection connection, string column)
{
    await using var command = new MySqlCommand(
        """
        SELECT COUNT(*)
        FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND LOWER(TABLE_NAME) = 'articoli'
          AND LOWER(COLUMN_NAME) = LOWER(@column);
        """,
        connection);
    command.Parameters.AddWithValue("@column", column);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
}

if (await ColumnExistsAsync(connection, "Uma"))
{
    Console.WriteLine("Uma: già presente");
    return 0;
}

if (!await ColumnExistsAsync(connection, "Ums"))
{
    Console.Error.WriteLine("Colonna Ums non trovata.");
    return 4;
}

await using var renameCommand = new MySqlCommand(
    "ALTER TABLE `Articoli` RENAME COLUMN `Ums` TO `Uma`;",
    connection);
await renameCommand.ExecuteNonQueryAsync();
Console.WriteLine("Articoli.Ums rinominata in Articoli.Uma.");
return 0;
