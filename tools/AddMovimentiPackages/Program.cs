using Microsoft.Extensions.Configuration;
using MySqlConnector;

const string databaseName = "MicroFish_0001";
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<SecretsMarker>(optional: false)
    .Build();
var baseConnectionString = configuration.GetConnectionString("MicronoteServer")
    ?? configuration.GetConnectionString("MicronoteDb")
    ?? throw new InvalidOperationException("Connessione MySQL non configurata.");
var connectionString = new MySqlConnectionStringBuilder(baseConnectionString)
{
    Database = databaseName
}.ConnectionString;

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();
if (!connection.Database.Equals(databaseName, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException($"Database inatteso: {connection.Database}.");

await using (var existsCommand = new MySqlCommand(
    """
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA=DATABASE()
      AND TABLE_NAME='Movimenti'
      AND COLUMN_NAME='Colli';
    """,
    connection))
{
    var exists = Convert.ToInt32(await existsCommand.ExecuteScalarAsync()) > 0;
    if (!exists)
    {
        await using var alterCommand = new MySqlCommand(
            """
            ALTER TABLE Movimenti
                ADD COLUMN Colli SMALLINT NOT NULL DEFAULT 0
                AFTER Articolo;
            """,
            connection);
        await alterCommand.ExecuteNonQueryAsync();
    }
}

await using (var verify = new MySqlCommand(
    """
    SELECT COLUMN_TYPE
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA=DATABASE()
      AND TABLE_NAME='Movimenti'
      AND COLUMN_NAME='Colli';
    """,
    connection))
{
    var columnType = Convert.ToString(await verify.ExecuteScalarAsync()) ?? "";
    if (!columnType.Equals("smallint", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Tipo inatteso per Movimenti.Colli: {columnType}.");
    Console.WriteLine($"Movimenti.Colli disponibile: {columnType} con segno.");
}

internal sealed class SecretsMarker;
