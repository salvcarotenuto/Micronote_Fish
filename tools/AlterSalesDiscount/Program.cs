using Microsoft.Extensions.Configuration;
using MySqlConnector;

const string databaseName = "MicroFish_0001";
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<SecretsMarker>(optional: false)
    .Build();
var baseConnectionString =
    configuration.GetConnectionString("MicronoteServer")
    ?? configuration.GetConnectionString("MicronoteDb")
    ?? throw new InvalidOperationException("Connessione MySQL non configurata.");
var connectionString = new MySqlConnectionStringBuilder(baseConnectionString)
{
    Database = databaseName
}.ConnectionString;

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();
await using (var alter = new MySqlCommand(
    """
    ALTER TABLE VenditeRg
        DROP INDEX UX_VenditeRg_Legacy,
        DROP COLUMN Anno,
        DROP COLUMN Codice,
        DROP COLUMN Cliente,
        DROP COLUMN DataDoc,
        DROP COLUMN Fornitore;
    """,
    connection))
{
    await alter.ExecuteNonQueryAsync();
}

await using var verify = new MySqlCommand(
    """
    SELECT COLUMN_NAME
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'VenditeRg'
      AND COLUMN_NAME IN ('Anno', 'Codice', 'Cliente', 'DataDoc', 'Fornitore');
    """,
    connection);
await using var reader = await verify.ExecuteReaderAsync();
if (await reader.ReadAsync())
{
    throw new InvalidOperationException(
        $"La colonna VenditeRg.{reader.GetString(0)} risulta ancora presente.");
}
await reader.DisposeAsync();

await using var integrity = new MySqlCommand(
    """
    SELECT
        (SELECT COUNT(*) FROM Vendite),
        (SELECT COUNT(*) FROM VenditeRg),
        (SELECT COUNT(*) FROM VenditeRg r
         LEFT JOIN Vendite v ON v.ID = r.ID
         WHERE v.ID IS NULL);
    """,
    connection);
await using var integrityReader = await integrity.ExecuteReaderAsync();
await integrityReader.ReadAsync();
Console.WriteLine(
    $"Verifica: Vendite={integrityReader.GetInt64(0)}, VenditeRg={integrityReader.GetInt64(1)}, " +
    $"righe orfane={integrityReader.GetInt64(2)}.");

internal sealed class SecretsMarker;
