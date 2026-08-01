using Microsoft.Extensions.Configuration;
using MySqlConnector;

const string companyDatabase = "MicroFish_0001";
const string optionKey = "DataUltimaElaborazioneNoteClienti";

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<SecretsMarker>(optional: false)
    .Build();
var configuredConnection =
    configuration.GetConnectionString("MicronoteServer")
    ?? configuration.GetConnectionString("MicronoteDb")
    ?? throw new InvalidOperationException("Connessione MySQL non configurata.");
var connectionString = new MySqlConnectionStringBuilder(configuredConnection)
{
    Database = companyDatabase
}.ConnectionString;

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();
await using var command = new MySqlCommand(
    """
    INSERT INTO Opzioni (Chiave, Valore)
    SELECT @key, ''
    WHERE NOT EXISTS (
        SELECT 1 FROM Opzioni WHERE Chiave = @key
    );
    SELECT Valore FROM Opzioni WHERE Chiave = @key LIMIT 1;
    """,
    connection);
command.Parameters.AddWithValue("@key", optionKey);
var value = Convert.ToString(await command.ExecuteScalarAsync());
Console.WriteLine($"{companyDatabase}.Opzioni: {optionKey} = '{value}'.");

internal sealed class SecretsMarker;
