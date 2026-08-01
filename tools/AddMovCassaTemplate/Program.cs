using Microsoft.Extensions.Configuration;
using MySqlConnector;

const string masterDatabase = "MicroFish_Master";
const string templateTable = "Schema_MovCassa";

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
await using var transaction = await connection.BeginTransactionAsync();

var createSql = $"""
    CREATE TABLE IF NOT EXISTS `{templateTable}` (
        `ID` INT NOT NULL AUTO_INCREMENT,
        `Anno` SMALLINT NULL DEFAULT 0,
        `Settore` TINYINT NULL DEFAULT 0,
        `DataMov` DATETIME NULL DEFAULT NULL,
        `Causale` SMALLINT NULL DEFAULT 0,
        `TipoMov` VARCHAR(1) NULL DEFAULT '',
        `CliFor` VARCHAR(1) NULL DEFAULT '',
        `Ditta` INT NULL DEFAULT 0,
        `Importo` FLOAT NULL DEFAULT 0,
        `ModoPag` TINYINT NULL DEFAULT 0,
        `Documento` INT NULL DEFAULT 0,
        `Descrizione` VARCHAR(100) NULL DEFAULT '',
        PRIMARY KEY (`ID`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
    """;
await using (var create = new MySqlCommand(createSql, connection, transaction))
{
    await create.ExecuteNonQueryAsync();
}

var version = DateTime.Now;
const string versionSql = """
    INSERT INTO Parametri (Chiave, VersioneSchemaDatabase)
    VALUES ('VersioneSchemaDatabase', @version)
    ON DUPLICATE KEY UPDATE VersioneSchemaDatabase = @version;

    UPDATE Aziende
    SET VersioneDbRichiesta = @version;
    """;
await using (var update = new MySqlCommand(versionSql, connection, transaction))
{
    update.Parameters.AddWithValue("@version", version);
    await update.ExecuteNonQueryAsync();
}

await transaction.CommitAsync();
Console.WriteLine($"{masterDatabase}.{templateTable} creato/verificato.");
Console.WriteLine($"Versione schema richiesta: {version:yyyy-MM-dd HH:mm:ss}.");

internal sealed class SecretsMarker;
