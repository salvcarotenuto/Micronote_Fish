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

await using (var rangeCommand = new MySqlCommand(
    "SELECT COALESCE(MIN(Colli),0), COALESCE(MAX(Colli),0) FROM VenditeRg;",
    connection))
await using (var reader = await rangeCommand.ExecuteReaderAsync())
{
    await reader.ReadAsync();
    var minimum = reader.GetInt32(0);
    var maximum = reader.GetInt32(1);
    if (minimum < -32000 || maximum > 32000)
        throw new InvalidOperationException(
            $"VenditeRg.Colli contiene valori fuori dal limite applicativo: min={minimum}, max={maximum}.");
    Console.WriteLine($"Intervallo Colli verificato: {minimum}..{maximum}.");
}

await using (var alterCommand = new MySqlCommand(
    "ALTER TABLE VenditeRg MODIFY Colli SMALLINT NOT NULL DEFAULT 0;",
    connection))
{
    await alterCommand.ExecuteNonQueryAsync();
}

await using (var verifyCommand = new MySqlCommand(
    """
    SELECT COLUMN_TYPE
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'VenditeRg'
      AND COLUMN_NAME = 'Colli';
    """,
    connection))
{
    var columnType = Convert.ToString(await verifyCommand.ExecuteScalarAsync()) ?? "";
    if (!columnType.Equals("smallint", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Tipo finale inatteso per VenditeRg.Colli: {columnType}.");
    Console.WriteLine($"VenditeRg.Colli aggiornato: {columnType} con segno.");
}

internal sealed class SecretsMarker;
