using Microsoft.Extensions.Configuration;
using MySqlConnector;

const string masterDatabase = "MicroFish_Master";
const string templateTable = "Schema_Articoli";
const string columnName = "GiacinP";

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

await using (var exists = new MySqlCommand(
    """
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE LOWER(TABLE_SCHEMA) = LOWER(@database)
      AND LOWER(TABLE_NAME) = LOWER(@table)
      AND LOWER(COLUMN_NAME) = LOWER(@column);
    """,
    connection))
{
    exists.Parameters.AddWithValue("@database", masterDatabase);
    exists.Parameters.AddWithValue("@table", templateTable);
    exists.Parameters.AddWithValue("@column", columnName);
    if (Convert.ToInt32(await exists.ExecuteScalarAsync()) != 1)
    {
        throw new InvalidOperationException($"Colonna {masterDatabase}.{templateTable}.{columnName} non trovata.");
    }
}

await using (var alter = new MySqlCommand(
    $"ALTER TABLE `{templateTable}` MODIFY COLUMN `{columnName}` DECIMAL(12,3) NULL DEFAULT 0.000;",
    connection))
{
    await alter.ExecuteNonQueryAsync();
}

var version = DateTime.Now;
await using (var updateVersion = new MySqlCommand(
    """
    INSERT INTO Parametri (Chiave, VersioneSchemaDatabase)
    VALUES ('VersioneSchemaDatabase', @version)
    ON DUPLICATE KEY UPDATE VersioneSchemaDatabase = @version;

    UPDATE Aziende
    SET VersioneDbRichiesta = @version;
    """,
    connection))
{
    updateVersion.Parameters.AddWithValue("@version", version);
    await updateVersion.ExecuteNonQueryAsync();
}

await using var verify = new MySqlCommand(
    """
    SELECT COLUMN_TYPE
    FROM information_schema.COLUMNS
    WHERE LOWER(TABLE_SCHEMA) = LOWER(@database)
      AND LOWER(TABLE_NAME) = LOWER(@table)
      AND LOWER(COLUMN_NAME) = LOWER(@column);
    """,
    connection);
verify.Parameters.AddWithValue("@database", masterDatabase);
verify.Parameters.AddWithValue("@table", templateTable);
verify.Parameters.AddWithValue("@column", columnName);
var columnType = Convert.ToString(await verify.ExecuteScalarAsync()) ?? "";

Console.WriteLine($"COLONNA={masterDatabase}.{templateTable}.{columnName}");
Console.WriteLine($"TIPO={columnType}");
Console.WriteLine($"VERSIONE={version:yyyy-MM-dd HH:mm:ss}");

internal sealed class SecretsMarker;
