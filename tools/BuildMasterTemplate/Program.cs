using Microsoft.Extensions.Configuration;
using MySqlConnector;

const string fixedPrefix = "Fixed_";
const string schemaPrefix = "Schema_";
var advanceVersionOnly = args.Contains("--advance-version", StringComparer.OrdinalIgnoreCase);
var showStatusOnly = args.Contains("--status", StringComparer.OrdinalIgnoreCase);
var sourceDatabase = args.ElementAtOrDefault(0) ?? "MicroFish_0001";
if ((advanceVersionOnly || showStatusOnly) && sourceDatabase.StartsWith("--", StringComparison.Ordinal))
{
    sourceDatabase = "MicroFish_0001";
}
var masterDatabase = args.ElementAtOrDefault(1) ?? "MicroFish_Master";
var fixedListPath = args.ElementAtOrDefault(2)
    ?? Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "..",
        "Documenti", "Tabelle fisse.txt"));
var fixedTables = File.ReadAllLines(fixedListPath)
    .Select(value => value.Trim())
    .Where(value => value.Length > 0)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
if (fixedTables.Length != 27)
{
    throw new InvalidOperationException(
        $"Il catalogo delle tabelle fisse contiene {fixedTables.Length} nomi; attesi 27.");
}

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<SecretsMarker>(optional: false)
    .Build();
var baseConnectionString =
    configuration.GetConnectionString("MicronoteServer")
    ?? configuration.GetConnectionString("MicronoteDb")
    ?? throw new InvalidOperationException("Connessione MySQL non configurata.");
var connectionString = new MySqlConnectionStringBuilder(baseConnectionString)
{
    Database = ""
}.ConnectionString;

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();
await RequireDatabaseAsync(sourceDatabase);
await RequireDatabaseAsync(masterDatabase);
await AlignVersionColumnsAsync();

if (showStatusOnly)
{
    await using var command = new MySqlCommand(
        $"""
        SELECT Codice, NomeDatabase, VersioneDbAttuale, VersioneDbRichiesta
        FROM {Q(masterDatabase)}.`Aziende`
        WHERE LOWER(NomeDatabase) = LOWER(@sourceDatabase)
        LIMIT 1;
        """,
        connection);
    command.Parameters.AddWithValue("@sourceDatabase", sourceDatabase);
    await using var reader = await command.ExecuteReaderAsync();
    if (await reader.ReadAsync())
    {
        Console.WriteLine(
            $"Azienda {reader.GetInt32(0):0000}: attuale=" +
            $"{(reader.IsDBNull(2) ? "NULL" : reader.GetDateTime(2).ToString("yyyy-MM-dd HH:mm:ss"))}, " +
            $"richiesta={(reader.IsDBNull(3) ? "NULL" : reader.GetDateTime(3).ToString("yyyy-MM-dd HH:mm:ss"))}.");
    }
    await reader.DisposeAsync();

    await using var backupCommand = new MySqlCommand(
        """
        SELECT SCHEMA_NAME
        FROM information_schema.SCHEMATA
        WHERE SCHEMA_NAME LIKE @backupPattern
        ORDER BY SCHEMA_NAME DESC
        LIMIT 5;
        """,
        connection);
    backupCommand.Parameters.AddWithValue("@backupPattern", sourceDatabase + "_Backup_%");
    await using var backupReader = await backupCommand.ExecuteReaderAsync();
    while (await backupReader.ReadAsync())
    {
        Console.WriteLine("Backup: " + backupReader.GetString(0));
    }
    return;
}

if (advanceVersionOnly)
{
    var version = DateTime.Now;
    await using var command = new MySqlCommand(
        $"""
        INSERT INTO {Q(masterDatabase)}.`Parametri`
            (Chiave, VersioneSchemaDatabase)
        VALUES ('VersioneSchemaDatabase', @version)
        ON DUPLICATE KEY UPDATE VersioneSchemaDatabase = @version;

        UPDATE {Q(masterDatabase)}.`Aziende`
        SET VersioneDbRichiesta = @version;
        """,
        connection);
    command.Parameters.AddWithValue("@version", version);
    await command.ExecuteNonQueryAsync();
    Console.WriteLine($"Versione richiesta avanzata a {version:yyyy-MM-dd HH:mm:ss}.");
    return;
}

var sourceTables = await ReadTablesAsync(sourceDatabase);
var missingFixed = fixedTables
    .Where(table => !sourceTables.Contains(table, StringComparer.OrdinalIgnoreCase))
    .ToArray();
if (missingFixed.Length > 0)
{
    throw new InvalidOperationException(
        "Tabelle fisse mancanti nel database aziendale: " + string.Join(", ", missingFixed));
}

var companyTables = sourceTables
    .Where(IsApplicationTable)
    .Where(table => !table.Equals("FixedTable", StringComparison.OrdinalIgnoreCase))
    .ToArray();
var existingTemplates = (await ReadTablesAsync(masterDatabase))
    .Where(table =>
        table.StartsWith(fixedPrefix, StringComparison.OrdinalIgnoreCase)
        || table.StartsWith(schemaPrefix, StringComparison.OrdinalIgnoreCase))
    .ToArray();

await ExecuteAsync(
    $"""
    CREATE TABLE IF NOT EXISTS {Q(masterDatabase)}.`Accessi` (
        Utente INT UNSIGNED NOT NULL,
        Azienda INT UNSIGNED NOT NULL,
        TheDate DATE NULL,
        TheTime TIME NULL
    );
    CREATE TABLE IF NOT EXISTS {Q(masterDatabase)}.`FixedTable` (
        Nome VARCHAR(254) NOT NULL,
        Descrizione VARCHAR(254) NULL,
        Record INT NULL,
        UNIQUE KEY UX_FixedTable_Nome (Nome)
    );
    """);

await ExecuteAsync("SET FOREIGN_KEY_CHECKS = 0;");
try
{
    foreach (var template in existingTemplates)
    {
        await ExecuteAsync($"DROP TABLE {Q(masterDatabase)}.{Q(template)};");
    }

    await ExecuteAsync($"DELETE FROM {Q(masterDatabase)}.`FixedTable`;");
    foreach (var table in fixedTables.Append("FixedTable"))
    {
        var rows = table.Equals("FixedTable", StringComparison.OrdinalIgnoreCase)
            ? fixedTables.Length + 1
            : await CountRowsAsync(sourceDatabase, table);
        await using var insert = new MySqlCommand(
            $"""
            INSERT INTO {Q(masterDatabase)}.`FixedTable`
                (Nome, Descrizione, Record)
            VALUES (@name, NULL, @rows);
            """,
            connection);
        insert.Parameters.AddWithValue("@name", table);
        insert.Parameters.AddWithValue("@rows", rows);
        await insert.ExecuteNonQueryAsync();
    }

    foreach (var table in companyTables)
    {
        var isFixed = fixedTables.Contains(table, StringComparer.OrdinalIgnoreCase);
        var template = (isFixed ? fixedPrefix : schemaPrefix) + table;
        await ExecuteAsync(
            $"CREATE TABLE {Q(masterDatabase)}.{Q(template)} LIKE {Q(sourceDatabase)}.{Q(table)};");
        if (isFixed)
        {
            await ExecuteAsync(
                $"INSERT INTO {Q(masterDatabase)}.{Q(template)} SELECT * FROM {Q(sourceDatabase)}.{Q(table)};");
        }
    }
}
finally
{
    await ExecuteAsync("SET FOREIGN_KEY_CHECKS = 1;");
}

var fixedTemplateCount = await CountTemplatesAsync(fixedPrefix);
var schemaTemplateCount = await CountTemplatesAsync(schemaPrefix);
if (fixedTemplateCount != fixedTables.Length)
{
    throw new InvalidOperationException(
        $"Verifica non superata: template Fixed={fixedTemplateCount}, attesi {fixedTables.Length}.");
}

var schemaVersion = DateTime.Now;
await using (var versionCommand = new MySqlCommand(
    $"""
    INSERT INTO {Q(masterDatabase)}.`Parametri`
        (Chiave, VersioneSchemaDatabase)
    VALUES ('VersioneSchemaDatabase', @version)
    ON DUPLICATE KEY UPDATE VersioneSchemaDatabase = @version;

    UPDATE {Q(masterDatabase)}.`Aziende`
    SET VersioneDbRichiesta = @version;

    UPDATE {Q(masterDatabase)}.`Aziende`
    SET VersioneDbAttuale = @version
    WHERE LOWER(NomeDatabase) = LOWER(@sourceDatabase);
    """,
    connection))
{
    versionCommand.Parameters.AddWithValue("@version", schemaVersion);
    versionCommand.Parameters.AddWithValue("@sourceDatabase", sourceDatabase);
    await versionCommand.ExecuteNonQueryAsync();
}

Console.WriteLine(
    $"{masterDatabase}: Accessi, FixedTable, {fixedTemplateCount} Fixed e " +
    $"{schemaTemplateCount} strutture aziendali.");
Console.WriteLine($"Versione schema: {schemaVersion:yyyy-MM-dd HH:mm:ss}.");
Console.WriteLine(
    "Schema: " + string.Join(
        ", ",
        companyTables
            .Where(table => !fixedTables.Contains(table, StringComparer.OrdinalIgnoreCase))
            .OrderBy(table => table, StringComparer.OrdinalIgnoreCase)));

async Task RequireDatabaseAsync(string database)
{
    await using var command = new MySqlCommand(
        "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = @name;",
        connection);
    command.Parameters.AddWithValue("@name", database);
    if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
    {
        throw new InvalidOperationException($"Database non trovato: {database}.");
    }
}

async Task AlignVersionColumnsAsync()
{
    foreach (var column in new[] { "VersioneDbAttuale", "VersioneDbRichiesta" })
    {
        await using var typeCommand = new MySqlCommand(
            """
            SELECT DATA_TYPE
            FROM information_schema.COLUMNS
            WHERE LOWER(TABLE_SCHEMA) = LOWER(@database)
              AND LOWER(TABLE_NAME) = 'aziende'
              AND LOWER(COLUMN_NAME) = LOWER(@column)
            LIMIT 1;
            """,
            connection);
        typeCommand.Parameters.AddWithValue("@database", masterDatabase);
        typeCommand.Parameters.AddWithValue("@column", column);
        var dataType = Convert.ToString(await typeCommand.ExecuteScalarAsync());
        if (string.Equals(dataType, "datetime", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var temporary = column + "DataOra";
        await ExecuteAsync(
            $"""
            ALTER TABLE {Q(masterDatabase)}.`Aziende`
                ADD COLUMN {Q(temporary)} DATETIME NULL;
            UPDATE {Q(masterDatabase)}.`Aziende`
            SET {Q(temporary)} = COALESCE(
                STR_TO_DATE({Q(column)}, '%Y-%m-%d.%H-%i'),
                STR_TO_DATE({Q(column)}, '%Y-%m-%d %H:%i:%s'));
            ALTER TABLE {Q(masterDatabase)}.`Aziende`
                DROP COLUMN {Q(column)},
                CHANGE COLUMN {Q(temporary)} {Q(column)} DATETIME NULL;
            """);
    }

    if (!await ColumnExistsAsync(masterDatabase, "Parametri", "VersioneSchemaDatabase"))
    {
        await ExecuteAsync(
            $"ALTER TABLE {Q(masterDatabase)}.`Parametri` " +
            "ADD COLUMN VersioneSchemaDatabase DATETIME NULL;");
    }

    await ExecuteAsync(
        $"""
        UPDATE {Q(masterDatabase)}.`Parametri`
        SET VersioneSchemaDatabase = COALESCE(
            VersioneSchemaDatabase,
            STR_TO_DATE(Valore, '%Y-%m-%d.%H-%i'),
            STR_TO_DATE(Valore, '%Y-%m-%d %H:%i:%s'))
        WHERE Chiave = 'VersioneSchemaDatabase';
        """);
}

async Task<bool> ColumnExistsAsync(string database, string table, string column)
{
    await using var command = new MySqlCommand(
        """
        SELECT COUNT(*)
        FROM information_schema.COLUMNS
        WHERE LOWER(TABLE_SCHEMA) = LOWER(@database)
          AND LOWER(TABLE_NAME) = LOWER(@table)
          AND LOWER(COLUMN_NAME) = LOWER(@column);
        """,
        connection);
    command.Parameters.AddWithValue("@database", database);
    command.Parameters.AddWithValue("@table", table);
    command.Parameters.AddWithValue("@column", column);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
}

async Task<IReadOnlyList<string>> ReadTablesAsync(string database)
{
    await using var command = new MySqlCommand(
        """
        SELECT TABLE_NAME
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = @database
          AND TABLE_TYPE = 'BASE TABLE'
        ORDER BY TABLE_NAME;
        """,
        connection);
    command.Parameters.AddWithValue("@database", database);
    var tables = new List<string>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        tables.Add(reader.GetString(0));
    }
    return tables;
}

async Task<long> CountRowsAsync(string database, string table)
{
    await using var command = new MySqlCommand(
        $"SELECT COUNT(*) FROM {Q(database)}.{Q(table)};",
        connection);
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}

async Task<int> CountTemplatesAsync(string prefix)
{
    await using var command = new MySqlCommand(
        """
        SELECT COUNT(*)
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = @database
          AND TABLE_TYPE = 'BASE TABLE'
          AND LOWER(LEFT(TABLE_NAME, CHAR_LENGTH(@prefix))) = LOWER(@prefix);
        """,
        connection);
    command.Parameters.AddWithValue("@database", masterDatabase);
    command.Parameters.AddWithValue("@prefix", prefix);
    return Convert.ToInt32(await command.ExecuteScalarAsync());
}

async Task ExecuteAsync(string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}

static bool IsApplicationTable(string table) =>
    !table.Contains("_bak_", StringComparison.OrdinalIgnoreCase)
    && !table.Contains("_backup_", StringComparison.OrdinalIgnoreCase)
    && !table.Contains("_Pre", StringComparison.OrdinalIgnoreCase)
    && !table.Contains("_Tmp_", StringComparison.OrdinalIgnoreCase);

static string Q(string identifier) => $"`{identifier.Replace("`", "``")}`";

internal sealed class SecretsMarker;
