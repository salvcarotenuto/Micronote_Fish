using Microsoft.Extensions.Configuration;
using MySqlConnector;

const string masterDatabase = "MicroFish_Master";
const string templateTable = "Schema_CaricoRg";
const string requiredColumn = "Colli";

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

await using (var verify = new MySqlCommand(
    """
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = @database
      AND TABLE_NAME = @table
      AND COLUMN_NAME = @column;
    """, connection))
{
    verify.Parameters.AddWithValue("@database", masterDatabase);
    verify.Parameters.AddWithValue("@table", templateTable);
    verify.Parameters.AddWithValue("@column", requiredColumn);
    if (Convert.ToInt32(await verify.ExecuteScalarAsync()) != 1)
        throw new InvalidOperationException($"{masterDatabase}.{templateTable}.{requiredColumn} non esiste: versione non aggiornata.");
}

var version = DateTime.Now;
await using var transaction = await connection.BeginTransactionAsync();
try
{
    const string sql = """
        INSERT INTO Parametri (Chiave, VersioneSchemaDatabase)
        VALUES ('VersioneSchemaDatabase', @version)
        ON DUPLICATE KEY UPDATE VersioneSchemaDatabase = @version;

        UPDATE Aziende
        SET VersioneDbRichiesta = @version;
        """;
    await using var update = new MySqlCommand(sql, connection, transaction);
    update.Parameters.AddWithValue("@version", version);
    await update.ExecuteNonQueryAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

Console.WriteLine($"Verificato: {masterDatabase}.{templateTable}.{requiredColumn}.");
Console.WriteLine($"Versione schema master: {version:yyyy-MM-dd HH:mm:ss}.");
Console.WriteLine("VersioneDbRichiesta aggiornata per tutte le aziende.");

internal sealed class SecretsMarker;
