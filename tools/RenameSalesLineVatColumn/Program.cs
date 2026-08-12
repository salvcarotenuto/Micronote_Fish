using Microsoft.Extensions.Configuration;
using MySqlConnector;

const string masterDatabase = "MicroFish_Master";
const string templateTable = "Schema_VenditeRg";

var configuration = new ConfigurationBuilder().AddUserSecrets<SecretsMarker>(optional: false).Build();
var configuredConnection = configuration.GetConnectionString("MicronoteServer")
    ?? configuration.GetConnectionString("MicronoteDb")
    ?? throw new InvalidOperationException("Connessione MySQL non configurata.");
var connectionString = new MySqlConnectionStringBuilder(configuredConnection) { Database = masterDatabase }.ConnectionString;

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();
await using var transaction = await connection.BeginTransactionAsync();
try
{
    var renamed = await RenameAsync(templateTable);
    var version = DateTime.Now;
    await using (var update = new MySqlCommand("""
        INSERT INTO Parametri (Chiave, VersioneSchemaDatabase)
        VALUES ('VersioneSchemaDatabase', @version)
        ON DUPLICATE KEY UPDATE VersioneSchemaDatabase = @version;
        UPDATE Aziende SET VersioneDbRichiesta = @version;
        """, connection, transaction))
    {
        update.Parameters.AddWithValue("@version", version);
        await update.ExecuteNonQueryAsync();
    }
    await transaction.CommitAsync();
    Console.WriteLine($"TABELLA={masterDatabase}.{templateTable}");
    Console.WriteLine($"RINOMINATA={(renamed ? "Sì" : "Già allineata")}");
    Console.WriteLine($"VERSIONE={version:yyyy-MM-dd HH:mm:ss}");
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

async Task<bool> RenameAsync(string table)
{
    var hasOld = await ColumnExistsAsync(table, "Iva");
    var hasNew = await ColumnExistsAsync(table, "AliqIva");
    if (hasNew && !hasOld) return false;
    if (!hasOld && !hasNew) throw new InvalidOperationException($"Né Iva né AliqIva esistono in {masterDatabase}.{table}.");
    if (hasOld && hasNew) throw new InvalidOperationException($"Entrambe le colonne Iva e AliqIva esistono in {masterDatabase}.{table}: rinomina interrotta.");
    await using var command = new MySqlCommand($"ALTER TABLE `{table}` CHANGE COLUMN `Iva` `AliqIva` DECIMAL(5,2) NULL DEFAULT 0;", connection, transaction);
    await command.ExecuteNonQueryAsync();
    return true;
}

async Task<bool> ColumnExistsAsync(string table, string column)
{
    const string sql = """
        SELECT COUNT(*) FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND COLUMN_NAME = @column;
        """;
    await using var command = new MySqlCommand(sql, connection, transaction);
    command.Parameters.AddWithValue("@table", table); command.Parameters.AddWithValue("@column", column);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
}

internal sealed class SecretsMarker;
