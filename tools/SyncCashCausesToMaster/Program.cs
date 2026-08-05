using Microsoft.Extensions.Configuration;
using MySqlConnector;

const string sourceDatabase = "MicroFish_0001";
const string masterDatabase = "MicroFish_Master";
const string sourceTable = "CausaliCassa";
const string masterTable = "Fixed_CausaliCassa";

var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var configuration = new ConfigurationBuilder().AddUserSecrets<SecretsMarker>(false).Build();
var configured = configuration.GetConnectionString("MicronoteServer")
    ?? configuration.GetConnectionString("MicronoteDb")
    ?? throw new InvalidOperationException("Connessione MySQL non configurata.");
var cs = new MySqlConnectionStringBuilder(configured) { Database = masterDatabase }.ConnectionString;

await using var connection = new MySqlConnection(cs);
await connection.OpenAsync();

var catalogRows = await ScalarAsync("""
    SELECT COUNT(*) FROM FixedTable
    WHERE LOWER(TRIM(Nome)) = LOWER('CausaliCassa');
    """);
var sourceRows = await ScalarAsync($"SELECT COUNT(*) FROM `{sourceDatabase}`.`{sourceTable}`;");
var masterExists = await ScalarAsync("""
    SELECT COUNT(*) FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = 'MicroFish_Master' AND TABLE_NAME = 'Fixed_CausaliCassa';
    """) > 0;
var masterRows = masterExists
    ? await ScalarAsync($"SELECT COUNT(*) FROM `{masterDatabase}`.`{masterTable}`;")
    : 0;
var currentVersion = await ValueAsync("""
    SELECT VersioneSchemaDatabase FROM Parametri
    WHERE Chiave = 'VersioneSchemaDatabase' LIMIT 1;
    """);

Console.WriteLine($"FixedTable contiene CausaliCassa: {(catalogRows > 0 ? "Sì" : "No")}");
Console.WriteLine($"{sourceDatabase}.{sourceTable}: {sourceRows} record");
Console.WriteLine($"{masterDatabase}.{masterTable}: {(masterExists ? $"{masterRows} record" : "assente")}");
Console.WriteLine($"Versione master corrente: {currentVersion}");

if (!apply) return;

await using var transaction = await connection.BeginTransactionAsync();
try
{
    var version = DateTime.Now;
    await ExecuteAsync($"""
        DROP TABLE IF EXISTS `{masterDatabase}`.`{masterTable}`;
        CREATE TABLE `{masterDatabase}`.`{masterTable}`
            LIKE `{sourceDatabase}`.`{sourceTable}`;
        INSERT INTO `{masterDatabase}`.`{masterTable}`
            SELECT * FROM `{sourceDatabase}`.`{sourceTable}`;
        DELETE FROM `{masterDatabase}`.`FixedTable`
            WHERE LOWER(TRIM(Nome)) = LOWER('CausaliCassa');
        INSERT INTO `{masterDatabase}`.`FixedTable` (Nome, Descrizione, Record)
            VALUES ('CausaliCassa', 'Causali dei movimenti di cassa', @rows);
        INSERT INTO `{masterDatabase}`.`Parametri` (Chiave, VersioneSchemaDatabase)
            VALUES ('VersioneSchemaDatabase', @version)
            ON DUPLICATE KEY UPDATE VersioneSchemaDatabase = @version;
        UPDATE `{masterDatabase}`.`Aziende`
            SET VersioneDbRichiesta = @version;
        """, ("@rows", sourceRows), ("@version", version));
    await transaction.CommitAsync();

    var copiedRows = await ScalarAsync($"SELECT COUNT(*) FROM `{masterDatabase}`.`{masterTable}`;");
    Console.WriteLine($"Copia completata: {copiedRows} record.");
    Console.WriteLine($"Versione master aggiornata: {version:yyyy-MM-dd HH:mm:ss}.");
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

async Task<int> ScalarAsync(string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    return Convert.ToInt32(await command.ExecuteScalarAsync());
}

async Task<string> ValueAsync(string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    var value = await command.ExecuteScalarAsync();
    return value is null || value is DBNull ? "non impostata" : Convert.ToDateTime(value).ToString("yyyy-MM-dd HH:mm:ss");
}

async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
{
    await using var command = new MySqlCommand(sql, connection, transaction);
    foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
    await command.ExecuteNonQueryAsync();
}

internal sealed class SecretsMarker;
