using Microsoft.Extensions.Configuration;
using MySqlConnector;

const int companyCode = 1;
const string masterDatabase = "MicroFish_Master";

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<SecretsMarker>(optional: false)
    .Build();
var configuredConnection =
    configuration.GetConnectionString("MicronoteServer")
    ?? configuration.GetConnectionString("MicronoteDb")
    ?? throw new InvalidOperationException("Connessione MySQL non configurata.");
var connectionString = new MySqlConnectionStringBuilder(configuredConnection)
{
    Database = masterDatabase
}.ConnectionString;

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

const string readSql = """
    SELECT p.VersioneSchemaDatabase,
           a.VersioneDbAttuale,
           a.VersioneDbRichiesta,
           COALESCE(a.NomeDatabase, '') AS NomeDatabase
    FROM Parametri p
    CROSS JOIN Aziende a
    WHERE p.Chiave = 'VersioneSchemaDatabase'
      AND a.Codice = @companyCode
    LIMIT 1;
    """;
await using var read = new MySqlCommand(readSql, connection);
read.Parameters.AddWithValue("@companyCode", companyCode);
await using var reader = await read.ExecuteReaderAsync();
if (!await reader.ReadAsync())
    throw new InvalidOperationException("Versioni master/azienda 0001 non trovate.");

var masterVersion = reader.IsDBNull(0) ? (DateTime?)null : reader.GetDateTime(0);
var currentVersion = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
var requiredBefore = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
var databaseName = reader.GetString(3);
await reader.DisposeAsync();

var isBehind = masterVersion.HasValue
    && (!currentVersion.HasValue || currentVersion.Value < masterVersion.Value);
var updateRequired = isBehind
    && (!requiredBefore.HasValue || requiredBefore.Value < masterVersion!.Value);

if (updateRequired)
{
    const string updateSql = """
        UPDATE Aziende
        SET VersioneDbRichiesta = @version
        WHERE Codice = @companyCode;
        """;
    await using var update = new MySqlCommand(updateSql, connection);
    update.Parameters.AddWithValue("@version", masterVersion!.Value);
    update.Parameters.AddWithValue("@companyCode", companyCode);
    await update.ExecuteNonQueryAsync();
}

Console.WriteLine($"Database: {databaseName}");
Console.WriteLine($"Versione master: {masterVersion:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine($"Versione attuale 0001: {currentVersion:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine($"Versione richiesta prima: {requiredBefore:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine($"Esito: {(updateRequired ? "VersioneDbRichiesta aggiornata alla versione master." : isBehind ? "VersioneDbRichiesta era già allineata al master." : "0001 non è arretrata rispetto al master.")}");

internal sealed class SecretsMarker;
