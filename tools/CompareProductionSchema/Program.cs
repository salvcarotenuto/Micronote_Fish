using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MySqlConnector;

const string localMasterDatabase = "Micronote_master";

var localConnectionString = Environment.GetEnvironmentVariable("MICRONOTE_LOCAL_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione locale mancante.");
var remoteConnectionString = Environment.GetEnvironmentVariable("MICRONOTE_REMOTE_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione remota mancante.");

localConnectionString = new MySqlConnectionStringBuilder(localConnectionString)
{
    Database = localMasterDatabase
}.ConnectionString;

await using var local = new MySqlConnection(localConnectionString);
await using var remote = new MySqlConnection(remoteConnectionString);
await local.OpenAsync();
await remote.OpenAsync();

var tableArgument = args.FirstOrDefault(arg =>
    arg.StartsWith("--table=", StringComparison.OrdinalIgnoreCase));
if (tableArgument is not null)
{
    var tableName = tableArgument["--table=".Length..].Trim();
    Console.WriteLine($"Tabella: {tableName}");
    Console.WriteLine($"Righe locali: {await CountRowsAsync(local, tableName)}");
    Console.WriteLine($"Righe remote: {await CountRowsAsync(remote, tableName)}");
    Console.WriteLine();
    Console.WriteLine("DEFINIZIONE LOCALE");
    Console.WriteLine(await ReadCreateTableAsync(local, tableName));
    Console.WriteLine();
    Console.WriteLine("DEFINIZIONE REMOTA");
    Console.WriteLine(await ReadCreateTableAsync(remote, tableName));
    return;
}

Console.WriteLine($"Modello locale: {local.Database}");
Console.WriteLine($"Database remoto: {remote.Database}");
Console.WriteLine();

var localTables = await ReadTablesAsync(local);
var remoteTables = await ReadTablesAsync(remote);
var missingRemote = localTables.Except(remoteTables, StringComparer.OrdinalIgnoreCase).Order().ToArray();
var extraRemote = remoteTables.Except(localTables, StringComparer.OrdinalIgnoreCase).Order().ToArray();
var commonTables = localTables.Intersect(remoteTables, StringComparer.OrdinalIgnoreCase).Order().ToArray();

Console.WriteLine($"Tabelle modello: {localTables.Count}");
Console.WriteLine($"Tabelle remote: {remoteTables.Count}");
Console.WriteLine($"Mancanti sul remoto: {missingRemote.Length}");
foreach (var name in missingRemote) Console.WriteLine($"  - {name}");
Console.WriteLine($"Aggiuntive sul remoto: {extraRemote.Length}");
foreach (var name in extraRemote) Console.WriteLine($"  + {name}");
Console.WriteLine();

var schemaDifferences = new List<string>();
foreach (var tableName in commonTables)
{
    var localColumns = await ReadColumnDefinitionsAsync(local, tableName);
    var remoteColumns = await ReadColumnDefinitionsAsync(remote, tableName);
    if (!localColumns.SequenceEqual(remoteColumns, StringComparer.OrdinalIgnoreCase))
    {
        schemaDifferences.Add($"{tableName}: colonne");
    }

    var localIndexes = await ReadIndexDefinitionsAsync(local, tableName);
    var remoteIndexes = await ReadIndexDefinitionsAsync(remote, tableName);
    if (!localIndexes.SequenceEqual(remoteIndexes, StringComparer.OrdinalIgnoreCase))
    {
        schemaDifferences.Add($"{tableName}: indici");
    }
}

Console.WriteLine($"Differenze strutturali: {schemaDifferences.Count}");
foreach (var difference in schemaDifferences) Console.WriteLine($"  * {difference}");
Console.WriteLine();

var fixedTables = await ReadFixedTableNamesAsync(local);
var fixedDifferences = new List<string>();
foreach (var tableName in fixedTables.Order(StringComparer.OrdinalIgnoreCase))
{
    if (!remoteTables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
    {
        fixedDifferences.Add($"{tableName}: tabella assente");
        continue;
    }

    var localSignature = await ReadDataSignatureAsync(local, tableName);
    var remoteSignature = await ReadDataSignatureAsync(remote, tableName);
    if (localSignature != remoteSignature)
    {
        fixedDifferences.Add(
            $"{tableName}: locale={localSignature.Rows} righe, remoto={remoteSignature.Rows} righe");
    }
}

Console.WriteLine($"Tabelle fixed controllate: {fixedTables.Count}");
Console.WriteLine($"Differenze dati fixed: {fixedDifferences.Count}");
foreach (var difference in fixedDifferences) Console.WriteLine($"  * {difference}");

var aligned = missingRemote.Length == 0
    && extraRemote.Length == 0
    && schemaDifferences.Count == 0
    && fixedDifferences.Count == 0;
Console.WriteLine();
Console.WriteLine(aligned
    ? "ESITO: database remoto allineato al modello locale."
    : "ESITO: database remoto non allineato; nessuna modifica eseguita.");

static async Task<IReadOnlyList<string>> ReadTablesAsync(MySqlConnection connection)
{
    await using var command = new MySqlCommand(
        """
        SELECT TABLE_NAME
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_TYPE = 'BASE TABLE'
        ORDER BY TABLE_NAME;
        """,
        connection);
    var names = new List<string>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync()) names.Add(reader.GetString(0));
    return names;
}

static async Task<IReadOnlyList<string>> ReadColumnDefinitionsAsync(
    MySqlConnection connection,
    string tableName)
{
    await using var command = new MySqlCommand(
        """
        SELECT ORDINAL_POSITION, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE,
               COALESCE(COLUMN_DEFAULT, '<NULL>'), EXTRA,
               COALESCE(CHARACTER_SET_NAME, ''), COALESCE(COLLATION_NAME, ''),
               COALESCE(GENERATION_EXPRESSION, '')
        FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName
        ORDER BY ORDINAL_POSITION;
        """,
        connection);
    command.Parameters.AddWithValue("@tableName", tableName);
    var definitions = new List<string>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        definitions.Add(string.Join(
            "\u001f",
            Enumerable.Range(0, reader.FieldCount)
                .Select(index => Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? "")));
    }
    return definitions;
}

static async Task<IReadOnlyList<string>> ReadIndexDefinitionsAsync(
    MySqlConnection connection,
    string tableName)
{
    await using var command = new MySqlCommand(
        """
        SELECT INDEX_NAME, NON_UNIQUE, SEQ_IN_INDEX, COALESCE(COLUMN_NAME, ''),
               COALESCE(SUB_PART, 0), INDEX_TYPE, COALESCE(EXPRESSION, '')
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName
        ORDER BY INDEX_NAME, SEQ_IN_INDEX;
        """,
        connection);
    command.Parameters.AddWithValue("@tableName", tableName);
    var definitions = new List<string>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        definitions.Add(string.Join(
            "\u001f",
            Enumerable.Range(0, reader.FieldCount)
                .Select(index => Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? "")));
    }
    return definitions;
}

static async Task<IReadOnlyList<string>> ReadFixedTableNamesAsync(MySqlConnection connection)
{
    await using var command = new MySqlCommand("SELECT * FROM FixedTable;", connection);
    var names = new List<string>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (reader.IsDBNull(index)) continue;
            var candidate = Convert.ToString(reader.GetValue(index))?.Trim();
            if (!string.IsNullOrWhiteSpace(candidate)
                && !names.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(candidate);
                break;
            }
        }
    }
    return names;
}

static async Task<(long Rows, string Hash)> ReadDataSignatureAsync(
    MySqlConnection connection,
    string tableName)
{
    await using var command = new MySqlCommand($"SELECT * FROM `{tableName.Replace("`", "``")}`;", connection);
    var serializedRows = new List<string>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var fields = new string[reader.FieldCount];
        for (var index = 0; index < reader.FieldCount; index++)
        {
            fields[index] = reader.IsDBNull(index)
                ? "<NULL>"
                : SerializeValue(reader.GetValue(index));
        }
        serializedRows.Add(string.Join("\u001f", fields));
    }
    serializedRows.Sort(StringComparer.Ordinal);
    var payload = string.Join("\u001e", serializedRows);
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    return (serializedRows.Count, hash);
}

static string SerializeValue(object value) => value switch
{
    byte[] bytes => Convert.ToBase64String(bytes),
    DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
    IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "",
    _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
};

static async Task<long> CountRowsAsync(MySqlConnection connection, string tableName)
{
    await using var command = new MySqlCommand(
        $"SELECT COUNT(*) FROM `{tableName.Replace("`", "``")}`;",
        connection);
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}

static async Task<string> ReadCreateTableAsync(MySqlConnection connection, string tableName)
{
    await using var command = new MySqlCommand(
        $"SHOW CREATE TABLE `{tableName.Replace("`", "``")}`;",
        connection);
    await using var reader = await command.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        throw new InvalidOperationException($"Tabella non trovata: {connection.Database}.{tableName}.");
    }
    return reader.GetString(1);
}
