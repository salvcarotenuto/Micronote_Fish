using System.Text;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

if (args.Length != 1 || !File.Exists(args[0]))
    throw new ArgumentException("Indicare il file TSV estratto da CausaliCont del database legacy.");

var lines = File.ReadAllLines(args[0]);
if (lines.Length < 2 || lines[0] != "Codice\tDescrizione\tTipo\tLocked\tDitta")
    throw new InvalidDataException("Formato dell'estratto CausaliCont legacy non riconosciuto.");

var configuration = new ConfigurationBuilder().AddUserSecrets<SecretsMarker>(false).Build();
var configuredConnection = configuration.GetConnectionString("MicronoteServer")
    ?? configuration.GetConnectionString("MicronoteDb")
    ?? throw new InvalidOperationException("Connessione MySQL non configurata.");
var cs = new MySqlConnectionStringBuilder(configuredConnection) { Database = "MicroFish_Master" }.ConnectionString;

await using var connection = new MySqlConnection(cs);
await connection.OpenAsync();

const string rebuildSql = """
    DROP TABLE IF EXISTS Fixed_CausaliCassa;
    CREATE TABLE Fixed_CausaliCassa (
        Codice SMALLINT DEFAULT 0,
        Descrizione VARCHAR(100) DEFAULT '',
        Tipo VARCHAR(1) DEFAULT '',
        Locked TINYINT DEFAULT 0,
        Ditta VARCHAR(1) DEFAULT ''
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
    """;
await using (var rebuild = new MySqlCommand(rebuildSql, connection))
    await rebuild.ExecuteNonQueryAsync();

const string insertSql = """
    INSERT INTO Fixed_CausaliCassa (Codice, Descrizione, Tipo, Locked, Ditta)
    VALUES (@codice, @descrizione, @tipo, @locked, @ditta);
    """;
var imported = 0;
foreach (var line in lines.Skip(1))
{
    var fields = line.Split('\t');
    if (fields.Length != 5) throw new InvalidDataException($"Riga legacy non valida: {imported + 2}.");
    await using var insert = new MySqlCommand(insertSql, connection);
    insert.Parameters.AddWithValue("@codice", DecodeNumber(fields[0]));
    insert.Parameters.AddWithValue("@descrizione", DecodeString(fields[1]));
    insert.Parameters.AddWithValue("@tipo", DecodeString(fields[2]));
    insert.Parameters.AddWithValue("@locked", DecodeNumber(fields[3]));
    insert.Parameters.AddWithValue("@ditta", DecodeString(fields[4]));
    await insert.ExecuteNonQueryAsync();
    imported++;
}

var version = DateTime.Now;
const string finalizeSql = """
    DELETE FROM FixedTable WHERE LOWER(TRIM(Nome)) = LOWER('CausaliCassa');
    INSERT INTO FixedTable (Nome, Descrizione, Record)
    VALUES ('CausaliCassa', 'Causali dei movimenti di cassa', @rows);
    INSERT INTO Parametri (Chiave, VersioneSchemaDatabase)
    VALUES ('VersioneSchemaDatabase', @version)
    ON DUPLICATE KEY UPDATE VersioneSchemaDatabase = @version;
    UPDATE Aziende SET VersioneDbRichiesta = @version;
    """;
await using (var finalize = new MySqlCommand(finalizeSql, connection))
{
    finalize.Parameters.AddWithValue("@rows", imported);
    finalize.Parameters.AddWithValue("@version", version);
    await finalize.ExecuteNonQueryAsync();
}

Console.WriteLine($"MicroFish_Master.Fixed_CausaliCassa ricreata dal legacy: {imported} record.");
Console.WriteLine($"Versione schema richiesta: {version:yyyy-MM-dd HH:mm:ss}.");

static int DecodeNumber(string value) =>
    value.StartsWith("V:", StringComparison.Ordinal) && int.TryParse(value[2..], out var result)
        ? result
        : 0;

static string DecodeString(string value) =>
    value.StartsWith("S:", StringComparison.Ordinal)
        ? Encoding.UTF8.GetString(Convert.FromBase64String(value[2..]))
        : "";

internal sealed class SecretsMarker;
