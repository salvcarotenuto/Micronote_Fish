using Microsoft.Extensions.Configuration;
using MySqlConnector;

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

var targets = new[]
{
    new Target("MicroFish_0001", "clienti"),
    new Target("MicroFish_0001", "fornitori"),
    new Target("MicroFish_Master", "Schema_clienti"),
    new Target("MicroFish_Master", "Schema_fornitori")
};

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();
var schemaChanged = false;

foreach (var target in targets)
{
    string? columnType = null;
    string? nullable = null;
    await using (var metadata = new MySqlCommand(
        """
        SELECT COLUMN_TYPE, IS_NULLABLE
        FROM information_schema.COLUMNS
        WHERE LOWER(TABLE_SCHEMA) = LOWER(@database)
          AND LOWER(TABLE_NAME) = LOWER(@table)
          AND LOWER(COLUMN_NAME) = 'fido'
        LIMIT 1;
        """,
        connection))
    {
        metadata.Parameters.AddWithValue("@database", target.Database);
        metadata.Parameters.AddWithValue("@table", target.Table);
        await using var reader = await metadata.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            columnType = reader.GetString(0);
            nullable = reader.GetString(1);
        }
    }

    if (columnType is null)
    {
        await ExecuteAsync(
            $"ALTER TABLE {Q(target.Database)}.{Q(target.Table)} " +
            "ADD COLUMN Fido DECIMAL(12,2) NULL;");
        schemaChanged = true;
    }
    else if (!string.Equals(nullable, "YES", StringComparison.OrdinalIgnoreCase))
    {
        await ExecuteAsync(
            $"ALTER TABLE {Q(target.Database)}.{Q(target.Table)} " +
            $"MODIFY COLUMN Fido {columnType} NULL;");
        schemaChanged = true;
    }
}

foreach (var target in targets)
{
    await using var verify = new MySqlCommand(
        """
        SELECT COLUMN_TYPE, IS_NULLABLE
        FROM information_schema.COLUMNS
        WHERE LOWER(TABLE_SCHEMA) = LOWER(@database)
          AND LOWER(TABLE_NAME) = LOWER(@table)
          AND LOWER(COLUMN_NAME) = 'fido'
        LIMIT 1;
        """,
        connection);
    verify.Parameters.AddWithValue("@database", target.Database);
    verify.Parameters.AddWithValue("@table", target.Table);
    await using var reader = await verify.ExecuteReaderAsync();
    if (!await reader.ReadAsync()
        || !reader.GetString(1).Equals("YES", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"La colonna {target.Database}.{target.Table}.Fido non risulta nullable.");
    }

    Console.WriteLine(
        $"{target.Database}.{target.Table}.Fido: {reader.GetString(0)}, NULL.");
}

if (schemaChanged)
{
    var schemaVersion = DateTime.Now;
    await using (var columnCommand = new MySqlCommand(
        """
        SELECT COUNT(*)
        FROM information_schema.COLUMNS
        WHERE LOWER(TABLE_SCHEMA) = 'microfish_master'
          AND LOWER(TABLE_NAME) = 'parametri'
          AND LOWER(COLUMN_NAME) = 'versioneschemadatabase';
        """,
        connection))
    {
        if (Convert.ToInt32(await columnCommand.ExecuteScalarAsync()) == 0)
        {
            await ExecuteAsync(
                "ALTER TABLE MicroFish_Master.Parametri " +
                "ADD COLUMN VersioneSchemaDatabase DATETIME NULL;");
        }
    }
    await using var versionCommand = new MySqlCommand(
        """
        INSERT INTO MicroFish_Master.Parametri
            (Chiave, VersioneSchemaDatabase)
        VALUES ('VersioneSchemaDatabase', @version)
        ON DUPLICATE KEY UPDATE VersioneSchemaDatabase = @version;

        UPDATE MicroFish_Master.Aziende
        SET VersioneDbRichiesta = @version;

        UPDATE MicroFish_Master.Aziende
        SET VersioneDbAttuale = @version
        WHERE LOWER(NomeDatabase) = 'microfish_0001';
        """,
        connection);
    versionCommand.Parameters.AddWithValue("@version", schemaVersion);
    await versionCommand.ExecuteNonQueryAsync();
    Console.WriteLine($"Versione schema aggiornata a {schemaVersion:yyyy-MM-dd HH:mm:ss}.");
}

async Task ExecuteAsync(string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}

static string Q(string identifier) => $"`{identifier.Replace("`", "``")}`";

internal sealed record Target(string Database, string Table);
internal sealed class SecretsMarker;
