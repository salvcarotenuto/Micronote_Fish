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
        `Codice` INT NULL DEFAULT 0,
        `DataMov` DATE NULL DEFAULT NULL,
        `Causale` SMALLINT NULL DEFAULT 0,
        `TipoMov` VARCHAR(1) NULL DEFAULT '',
        `CliFor` VARCHAR(1) NULL DEFAULT '',
        `Ditta` INT NULL DEFAULT 0,
        `Importo` DECIMAL(12,2) NULL DEFAULT 0,
        `ModoPag` TINYINT NULL DEFAULT 0,
        `TipoDocumento` VARCHAR(1) NULL DEFAULT '',
        `Documento` INT NULL DEFAULT 0,
        `PuntoV` SMALLINT NULL DEFAULT 0,
        `Annotazioni` VARCHAR(100) NULL DEFAULT '',
        PRIMARY KEY (`ID`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
    """;
await using (var create = new MySqlCommand(createSql, connection, transaction))
{
    await create.ExecuteNonQueryAsync();
}

if (!await ColumnExistsAsync(connection, transaction, templateTable, "TipoDocumento"))
{
    await using var addDocumentType = new MySqlCommand(
        "ALTER TABLE `Schema_MovCassa` ADD COLUMN `TipoDocumento` VARCHAR(1) NULL DEFAULT '' AFTER `ModoPag`;",
        connection,
        transaction);
    await addDocumentType.ExecuteNonQueryAsync();
}

if (!await ColumnExistsAsync(connection, transaction, templateTable, "Codice"))
{
    await using var addCode = new MySqlCommand(
        "ALTER TABLE `Schema_MovCassa` ADD COLUMN `Codice` INT NULL DEFAULT 0 AFTER `Settore`;",
        connection,
        transaction);
    await addCode.ExecuteNonQueryAsync();
}

if (!await ColumnExistsAsync(connection, transaction, templateTable, "PuntoV"))
{
    await using var addStore = new MySqlCommand(
        "ALTER TABLE `Schema_MovCassa` ADD COLUMN `PuntoV` SMALLINT NULL DEFAULT 0 AFTER `Documento`;",
        connection,
        transaction);
    await addStore.ExecuteNonQueryAsync();
}

if (await ColumnExistsAsync(connection, transaction, templateTable, "Descrizione")
    && !await ColumnExistsAsync(connection, transaction, templateTable, "Annotazioni"))
{
    await using var renameNotes = new MySqlCommand(
        "ALTER TABLE `Schema_MovCassa` CHANGE COLUMN `Descrizione` `Annotazioni` VARCHAR(100) NULL DEFAULT '';",
        connection,
        transaction);
    await renameNotes.ExecuteNonQueryAsync();
}

if (await ColumnExistsAsync(connection, transaction, templateTable, "Ammotazioni")
    && !await ColumnExistsAsync(connection, transaction, templateTable, "Annotazioni"))
{
    await using var correctNotesName = new MySqlCommand(
        "ALTER TABLE `Schema_MovCassa` CHANGE COLUMN `Ammotazioni` `Annotazioni` VARCHAR(100) NULL DEFAULT '';",
        connection,
        transaction);
    await correctNotesName.ExecuteNonQueryAsync();
}

await using (var alignColumns = new MySqlCommand(
    "ALTER TABLE `Schema_MovCassa` " +
    "MODIFY COLUMN `DataMov` DATE NULL DEFAULT NULL, " +
    "MODIFY COLUMN `Importo` DECIMAL(12,2) NULL DEFAULT 0;",
    connection,
    transaction))
{
    await alignColumns.ExecuteNonQueryAsync();
}

if (await IndexExistsAsync(
        connection,
        transaction,
        templateTable,
        "UX_Schema_MovCassa_Anno_Settore_Codice"))
{
    await using var dropBusinessKey = new MySqlCommand(
        "ALTER TABLE `Schema_MovCassa` DROP INDEX `UX_Schema_MovCassa_Anno_Settore_Codice`;",
        connection,
        transaction);
    await dropBusinessKey.ExecuteNonQueryAsync();
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

static async Task<bool> ColumnExistsAsync(
    MySqlConnection connection,
    MySqlTransaction transaction,
    string tableName,
    string columnName)
{
    const string sql = """
        SELECT COUNT(*)
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @tableName
          AND COLUMN_NAME = @columnName;
        """;
    await using var command = new MySqlCommand(sql, connection, transaction);
    command.Parameters.AddWithValue("@tableName", tableName);
    command.Parameters.AddWithValue("@columnName", columnName);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
}

static async Task<bool> IndexExistsAsync(
    MySqlConnection connection,
    MySqlTransaction transaction,
    string tableName,
    string indexName)
{
    const string sql = """
        SELECT COUNT(*)
        FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @tableName
          AND INDEX_NAME = @indexName;
        """;
    await using var command = new MySqlCommand(sql, connection, transaction);
    command.Parameters.AddWithValue("@tableName", tableName);
    command.Parameters.AddWithValue("@indexName", indexName);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
}

internal sealed class SecretsMarker;
