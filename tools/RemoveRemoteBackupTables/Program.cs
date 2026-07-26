using MySqlConnector;

var connectionString = Environment.GetEnvironmentVariable("MICRONOTE_REMOTE_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione remota mancante.");
var requestedTable = args
    .FirstOrDefault(arg => arg.StartsWith("--table=", StringComparison.OrdinalIgnoreCase))?
    ["--table=".Length..]
    .Trim();
var backupTables = string.IsNullOrWhiteSpace(requestedTable)
    ? new[]
{
    "carico_bak_autoid_20260629_115616",
    "caricorg_bak_autoid_20260629_115616"
}
    : new[] { requestedTable };

if (backupTables.Any(tableName =>
        !System.Text.RegularExpressions.Regex.IsMatch(
            tableName,
            @"^(carico|caricorg)_bak_autoid_\d{8}_\d{6}$|^MovIvaRg_PreSalesRealign_\d{8}_\d{6}$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
{
    throw new InvalidOperationException("Nome tabella di backup non autorizzato.");
}

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

var operationalTables = backupTables
    .Select(tableName =>
        tableName.StartsWith("MovIvaRg_", StringComparison.OrdinalIgnoreCase)
            ? "MovIvaRg"
            : tableName.StartsWith("CaricoRg_", StringComparison.OrdinalIgnoreCase)
                ? "CaricoRg"
                : "Carico")
    .Distinct(StringComparer.OrdinalIgnoreCase);
foreach (var operationalTable in operationalTables)
{
    if (!await TableExistsAsync(connection, operationalTable))
    {
        throw new InvalidOperationException(
            $"Tabella operativa assente: {operationalTable}. Nessuna cancellazione eseguita.");
    }
}

var rowCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
foreach (var tableName in backupTables)
{
    if (!await TableExistsAsync(connection, tableName))
    {
        throw new InvalidOperationException(
            $"Tabella di backup non trovata: {tableName}. Nessuna cancellazione eseguita.");
    }
    rowCounts[tableName] = await CountRowsAsync(connection, tableName);
}

foreach (var tableName in backupTables)
{
    await using var command = new MySqlCommand($"DROP TABLE {Q(tableName)};", connection);
    await command.ExecuteNonQueryAsync();
}

foreach (var tableName in backupTables)
{
    if (await TableExistsAsync(connection, tableName))
    {
        throw new InvalidOperationException($"Rimozione non riuscita: {tableName}.");
    }
    Console.WriteLine($"Rimossa {connection.Database}.{tableName} ({rowCounts[tableName]} righe).");
}

Console.WriteLine("Le tabelle operative collegate sono presenti.");

static async Task<bool> TableExistsAsync(MySqlConnection connection, string tableName)
{
    await using var command = new MySqlCommand(
        """
        SELECT COUNT(*)
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @tableName
          AND TABLE_TYPE = 'BASE TABLE';
        """,
        connection);
    command.Parameters.AddWithValue("@tableName", tableName);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
}

static async Task<long> CountRowsAsync(MySqlConnection connection, string tableName)
{
    await using var command = new MySqlCommand($"SELECT COUNT(*) FROM {Q(tableName)};", connection);
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}

static string Q(string identifier) => $"`{identifier.Replace("`", "``")}`";
