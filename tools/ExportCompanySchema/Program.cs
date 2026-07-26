using System.Text.RegularExpressions;
using MySqlConnector;

const string sourceDatabase = "Micronote_0001";
const string targetDatabase = "Micronote_master";
const string catalogTable = "FixedTable";

var connectionString = Environment.GetEnvironmentVariable("MICRONOTE_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione mancante.");
var apply = args.Any(arg => arg.Equals("--apply", StringComparison.OrdinalIgnoreCase));
var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

await RequireDatabaseAsync(connection, sourceDatabase);
await RequireDatabaseAsync(connection, targetDatabase);

var fixedTables = await ReadFixedTableNamesAsync(connection);
var sourceTables = await ReadBaseTableNamesAsync(connection, sourceDatabase);
var companyTables = sourceTables
    .Where(name => !fixedTables.Contains(name, StringComparer.OrdinalIgnoreCase))
    .Where(IsOperationalTable)
    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (companyTables.Length == 0)
{
    throw new InvalidOperationException("Nessuna tabella aziendale trovata.");
}

Console.WriteLine($"Sorgente: {sourceDatabase}");
Console.WriteLine($"Destinazione: {targetDatabase}");
Console.WriteLine($"Tabelle fisse escluse: {fixedTables.Count}");
Console.WriteLine($"Tabelle aziendali da definire: {companyTables.Length}");
Console.WriteLine();

var existingTargets = new List<string>();
foreach (var tableName in companyTables)
{
    var exists = await TableExistsAsync(connection, targetDatabase, tableName);
    if (exists)
    {
        existingTargets.Add(tableName);
    }
    Console.WriteLine($"{tableName}: master={(exists ? "già presente" : "da creare")}");
}

if (!apply)
{
    Console.WriteLine();
    Console.WriteLine($"Ricognizione superata: {companyTables.Length} definizioni, {existingTargets.Count} già presenti.");
    Console.WriteLine("Rieseguire con --apply per creare nel master le tabelle vuote.");
    return;
}

await ExecuteAsync(connection, "SET FOREIGN_KEY_CHECKS = 0;");
try
{
    foreach (var tableName in companyTables)
    {
        if (await TableExistsAsync(connection, targetDatabase, tableName))
        {
            var backupName = $"{tableName}_PreSchemaExport_{stamp}";
            await ExecuteAsync(
                connection,
                $"CREATE TABLE {Q(targetDatabase)}.{Q(backupName)} LIKE {Q(targetDatabase)}.{Q(tableName)};");
            await ExecuteAsync(
                connection,
                $"INSERT INTO {Q(targetDatabase)}.{Q(backupName)} SELECT * FROM {Q(targetDatabase)}.{Q(tableName)};");
            await ExecuteAsync(connection, $"DROP TABLE {Q(targetDatabase)}.{Q(tableName)};");
        }

        var createSql = await ReadCreateTableAsync(connection, sourceDatabase, tableName);
        createSql = Regex.Replace(
            createSql,
            @"\sAUTO_INCREMENT=\d+",
            string.Empty,
            RegexOptions.IgnoreCase);
        await connection.ChangeDatabaseAsync(targetDatabase);
        await ExecuteAsync(connection, createSql);

        var targetRows = await CountRowsAsync(connection, targetDatabase, tableName);
        if (targetRows != 0)
        {
            throw new InvalidOperationException(
                $"La tabella {targetDatabase}.{tableName} non è vuota dopo la creazione.");
        }
    }
}
finally
{
    await ExecuteAsync(connection, "SET FOREIGN_KEY_CHECKS = 1;");
}

var targetCompanyTables = await ReadBaseTableNamesAsync(connection, targetDatabase);
var missing = companyTables
    .Where(name => !targetCompanyTables.Contains(name, StringComparer.OrdinalIgnoreCase))
    .ToArray();
if (missing.Length != 0)
{
    throw new InvalidOperationException(
        $"Definizioni mancanti nel master: {string.Join(", ", missing)}.");
}

Console.WriteLine();
Console.WriteLine($"Esportazione completata: {companyTables.Length} tabelle aziendali create vuote.");
Console.WriteLine($"Tabelle complessive nel master: {targetCompanyTables.Count}.");

async Task<IReadOnlyList<string>> ReadFixedTableNamesAsync(MySqlConnection db)
{
    await using var command = new MySqlCommand(
        $"SELECT * FROM {Q(sourceDatabase)}.{Q(catalogTable)};",
        db);
    await using var reader = await command.ExecuteReaderAsync();
    var names = new List<string>();
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

static async Task<IReadOnlyList<string>> ReadBaseTableNamesAsync(
    MySqlConnection connection,
    string databaseName)
{
    await using var command = new MySqlCommand(
        """
        SELECT TABLE_NAME
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = @databaseName
          AND TABLE_TYPE = 'BASE TABLE'
        ORDER BY TABLE_NAME;
        """,
        connection);
    command.Parameters.AddWithValue("@databaseName", databaseName);
    var names = new List<string>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        names.Add(reader.GetString(0));
    }
    return names;
}

static async Task<string> ReadCreateTableAsync(
    MySqlConnection connection,
    string databaseName,
    string tableName)
{
    await using var command = new MySqlCommand(
        $"SHOW CREATE TABLE {Q(databaseName)}.{Q(tableName)};",
        connection);
    await using var reader = await command.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        throw new InvalidOperationException($"Definizione non trovata: {databaseName}.{tableName}.");
    }
    return reader.GetString(1);
}

static async Task RequireDatabaseAsync(MySqlConnection connection, string databaseName)
{
    await using var command = new MySqlCommand(
        "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = @name;",
        connection);
    command.Parameters.AddWithValue("@name", databaseName);
    if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
    {
        throw new InvalidOperationException($"Database non trovato: {databaseName}.");
    }
}

static async Task<bool> TableExistsAsync(
    MySqlConnection connection,
    string databaseName,
    string tableName)
{
    await using var command = new MySqlCommand(
        """
        SELECT COUNT(*)
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = @databaseName
          AND TABLE_NAME = @tableName
          AND TABLE_TYPE = 'BASE TABLE';
        """,
        connection);
    command.Parameters.AddWithValue("@databaseName", databaseName);
    command.Parameters.AddWithValue("@tableName", tableName);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
}

static async Task<long> CountRowsAsync(
    MySqlConnection connection,
    string databaseName,
    string tableName)
{
    await using var command = new MySqlCommand(
        $"SELECT COUNT(*) FROM {Q(databaseName)}.{Q(tableName)};",
        connection);
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}

static async Task ExecuteAsync(MySqlConnection connection, string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}

static bool IsOperationalTable(string tableName) =>
    !tableName.Contains("_PreSalesRealign_", StringComparison.OrdinalIgnoreCase)
    && !tableName.Contains("_PreFixedExport_", StringComparison.OrdinalIgnoreCase)
    && !tableName.Contains("_PreSchemaExport_", StringComparison.OrdinalIgnoreCase)
    && !tableName.Contains("_FixedExportTmp_", StringComparison.OrdinalIgnoreCase);

static string Q(string identifier) => $"`{identifier.Replace("`", "``")}`";
