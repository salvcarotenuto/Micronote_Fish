using System.Text.RegularExpressions;
using MySqlConnector;

const string sourceMaster = "Micronote_Master";
const string sourceCompany = "Micronote_0001";
const string targetMaster = "MicroFish_Master";
const string targetCompany = "MicroFish_0001";

var configuredConnection = Environment.GetEnvironmentVariable("MICROFISH_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione MySQL mancante.");
var builder = new MySqlConnectionStringBuilder(configuredConnection)
{
    Database = ""
};

await using var connection = new MySqlConnection(builder.ConnectionString);
await connection.OpenAsync();

await RequireDatabaseAsync(connection, sourceMaster);
await RequireDatabaseAsync(connection, sourceCompany);
await RequireDatabaseMissingAsync(connection, targetMaster);
await RequireDatabaseMissingAsync(connection, targetCompany);

var created = new List<string>();
try
{
    await CreateDatabaseAsync(connection, targetMaster);
    created.Add(targetMaster);
    await CreateDatabaseAsync(connection, targetCompany);
    created.Add(targetCompany);

    await CloneDatabaseStructureAsync(connection, sourceMaster, targetMaster);
    await CloneDatabaseStructureAsync(connection, sourceCompany, targetCompany);

    await ExecuteAsync(connection, $"""
        INSERT INTO {Q(targetMaster)}.`Aziende`
        (
            Codice, Nome, Password, Attiva, Bloccata, NomeDatabase,
            VersioneDbAttuale, VersioneDbRichiesta
        )
        VALUES
        (
            1, '0001', '0001', 1, 0, '{targetCompany}', NULL, NULL
        );
        """);

    var seedTables = await ReadFixedTableNamesAsync(connection, sourceCompany);
    foreach (var table in new[] { "FixedTable" }
        .Concat(seedTables)
        .Concat(new[] { "utenti" })
        .Distinct(StringComparer.OrdinalIgnoreCase))
    {
        if (await TableExistsAsync(connection, sourceCompany, table)
            && await TableExistsAsync(connection, targetCompany, table))
        {
            await CopyRowsAsync(connection, sourceCompany, targetCompany, table);
        }
    }

    Console.WriteLine($"Creato {targetMaster}.");
    Console.WriteLine($"Creato {targetCompany}.");
    Console.WriteLine($"Azienda 0001 registrata; tabelle fisse e utenti iniziali copiati.");
}
catch
{
    foreach (var database in created.AsEnumerable().Reverse())
    {
        await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS {Q(database)};");
    }
    throw;
}

static async Task CloneDatabaseStructureAsync(
    MySqlConnection connection,
    string source,
    string target)
{
    var tables = new List<string>();
    await using (var command = new MySqlCommand(
        """
        SELECT TABLE_NAME
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = @database
          AND TABLE_TYPE = 'BASE TABLE'
        ORDER BY TABLE_NAME;
        """,
        connection))
    {
        command.Parameters.AddWithValue("@database", source);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }
    }

    await ExecuteAsync(connection, "SET FOREIGN_KEY_CHECKS = 0;");
    try
    {
        foreach (var table in tables)
        {
            await ExecuteAsync(
                connection,
                $"CREATE TABLE {Q(target)}.{Q(table)} LIKE {Q(source)}.{Q(table)};");
        }
    }
    finally
    {
        await ExecuteAsync(connection, "SET FOREIGN_KEY_CHECKS = 1;");
    }
}

static async Task<IReadOnlyList<string>> ReadFixedTableNamesAsync(
    MySqlConnection connection,
    string database)
{
    if (!await TableExistsAsync(connection, database, "FixedTable"))
    {
        return [];
    }

    var names = new List<string>();
    await using var command = new MySqlCommand(
        $"SELECT * FROM {Q(database)}.`FixedTable`;",
        connection);
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (reader.IsDBNull(index))
            {
                continue;
            }

            var candidate = Convert.ToString(reader.GetValue(index))?.Trim();
            if (!string.IsNullOrWhiteSpace(candidate)
                && Regex.IsMatch(candidate, "^[A-Za-z0-9_]+$")
                && !names.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(candidate);
                break;
            }
        }
    }

    return names;
}

static async Task CopyRowsAsync(
    MySqlConnection connection,
    string source,
    string target,
    string table)
{
    await ExecuteAsync(
        connection,
        $"INSERT INTO {Q(target)}.{Q(table)} SELECT * FROM {Q(source)}.{Q(table)};");
}

static async Task CreateDatabaseAsync(MySqlConnection connection, string database) =>
    await ExecuteAsync(
        connection,
        $"CREATE DATABASE {Q(database)} CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;");

static async Task RequireDatabaseAsync(MySqlConnection connection, string database)
{
    if (!await DatabaseExistsAsync(connection, database))
    {
        throw new InvalidOperationException($"Database sorgente non trovato: {database}.");
    }
}

static async Task RequireDatabaseMissingAsync(MySqlConnection connection, string database)
{
    if (await DatabaseExistsAsync(connection, database))
    {
        throw new InvalidOperationException(
            $"Database destinazione già presente: {database}. Nessuna modifica eseguita.");
    }
}

static async Task<bool> DatabaseExistsAsync(MySqlConnection connection, string database)
{
    await using var command = new MySqlCommand(
        "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = @database;",
        connection);
    command.Parameters.AddWithValue("@database", database);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
}

static async Task<bool> TableExistsAsync(
    MySqlConnection connection,
    string database,
    string table)
{
    await using var command = new MySqlCommand(
        """
        SELECT COUNT(*)
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = @database
          AND TABLE_NAME = @table
          AND TABLE_TYPE = 'BASE TABLE';
        """,
        connection);
    command.Parameters.AddWithValue("@database", database);
    command.Parameters.AddWithValue("@table", table);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
}

static async Task ExecuteAsync(MySqlConnection connection, string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}

static string Q(string identifier) => $"`{identifier.Replace("`", "``")}`";
