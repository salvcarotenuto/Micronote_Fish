using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

if (args.Length != 1 || !File.Exists(args[0]))
    throw new ArgumentException("Indicare l'estratto TSV di MOVCASSA.");

var lines = File.ReadAllLines(args[0]);
const string expectedHeader = "Anno\tSettore\tCodice\tDataMov\tCausale\tTipoMov\tCliFor\tDitta\tImporto\tModoPag\tNumDoc\tDataDoc\tDescrizione";
if (lines.Length < 2 || lines[0] != expectedHeader)
    throw new InvalidDataException("Formato dell'estratto MOVCASSA non riconosciuto.");

var rows = new List<LegacyRow>(lines.Length - 1);
var keys = new HashSet<(int Year, int Sector, int Code)>();
foreach (var (line, index) in lines.Skip(1).Select((value, index) => (value, index)))
{
    var f = line.Split('\t');
    if (f.Length != 13) throw new InvalidDataException($"Riga {index + 2} non valida.");
    var row = new LegacyRow(
        Number(f[0]), Number(f[1]), Number(f[2]), Date(f[3]), Number(f[4]), Text(f[5]),
        Text(f[6]), Number(f[7]), decimal.Round(Decimal(f[8]), 2, MidpointRounding.AwayFromZero),
        Number(f[9]), Text(f[12]));
    if (!keys.Add((row.Year, row.Sector, row.Code)))
        throw new InvalidDataException($"Chiave legacy duplicata: {row.Year}/{row.Sector}/{row.Code}.");
    rows.Add(row);
}
if (rows.Count != 14144) throw new InvalidDataException($"Attesi 14144 record, trovati {rows.Count}.");

var configuration = new ConfigurationBuilder().AddUserSecrets<SecretsMarker>(false).Build();
var configured = configuration.GetConnectionString("MicronoteServer")
    ?? configuration.GetConnectionString("MicronoteDb")
    ?? throw new InvalidOperationException("Connessione MySQL non configurata.");
var cs = new MySqlConnectionStringBuilder(configured) { Database = "microfish_0001" }.ConnectionString;
await using var connection = new MySqlConnection(cs);
await connection.OpenAsync();

var existingKeys = new HashSet<(int Year, int Sector, int Code)>();
await using (var existing = new MySqlCommand("SELECT Anno, Settore, Codice FROM MovCassa;", connection))
await using (var reader = await existing.ExecuteReaderAsync())
    while (await reader.ReadAsync())
        existingKeys.Add((Convert.ToInt32(reader[0]), Convert.ToInt32(reader[1]), Convert.ToInt32(reader[2])));
var conflicts = keys.Intersect(existingKeys).ToArray();
if (conflicts.Length > 0)
    throw new InvalidOperationException($"Importazione annullata: {conflicts.Length} chiavi già presenti.");

var suffix = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
var backupTable = $"MovCassa_Backup_{suffix}";
await using (var backup = new MySqlCommand(
    $"CREATE TABLE `{backupTable}` LIKE MovCassa; INSERT INTO `{backupTable}` SELECT * FROM MovCassa;",
    connection))
    await backup.ExecuteNonQueryAsync();

var hasDescription = await ColumnExists(connection, "Descrizione");
var hasMisspelled = await ColumnExists(connection, "Ammotazioni");
var hasAnnotations = await ColumnExists(connection, "Annotazioni");
if (!hasAnnotations && hasDescription)
    await Execute(connection, "ALTER TABLE MovCassa CHANGE COLUMN Descrizione Annotazioni VARCHAR(100) NULL DEFAULT ''; ");
else if (!hasAnnotations && hasMisspelled)
    await Execute(connection, "ALTER TABLE MovCassa CHANGE COLUMN Ammotazioni Annotazioni VARCHAR(100) NULL DEFAULT ''; ");
await Execute(connection, "ALTER TABLE MovCassa MODIFY COLUMN DataMov DATE NULL DEFAULT NULL, MODIFY COLUMN Importo DECIMAL(12,2) NULL DEFAULT 0;");

await using var transaction = await connection.BeginTransactionAsync();
const string insertSql = """
    INSERT INTO MovCassa
        (Anno, Settore, Codice, DataMov, Causale, TipoMov, CliFor, Ditta,
         Importo, ModoPag, TipoDocumento, Documento, PuntoV, Annotazioni)
    VALUES
        (@year, @sector, @code, @date, @cause, @movementType, @subjectType, @subject,
         @amount, @payment, '', 0, 0, @notes);
    """;
await using var insert = new MySqlCommand(insertSql, connection, transaction);
insert.Parameters.Add("@year", MySqlDbType.Int16);
insert.Parameters.Add("@sector", MySqlDbType.Byte);
insert.Parameters.Add("@code", MySqlDbType.Int32);
insert.Parameters.Add("@date", MySqlDbType.Date);
insert.Parameters.Add("@cause", MySqlDbType.Int16);
insert.Parameters.Add("@movementType", MySqlDbType.VarChar);
insert.Parameters.Add("@subjectType", MySqlDbType.VarChar);
insert.Parameters.Add("@subject", MySqlDbType.Int32);
insert.Parameters.Add("@amount", MySqlDbType.Decimal);
insert.Parameters.Add("@payment", MySqlDbType.Byte);
insert.Parameters.Add("@notes", MySqlDbType.VarChar);
insert.Prepare();
try
{
    foreach (var row in rows)
    {
        insert.Parameters["@year"].Value = row.Year;
        insert.Parameters["@sector"].Value = row.Sector;
        insert.Parameters["@code"].Value = row.Code;
        insert.Parameters["@date"].Value = row.MovementDate;
        insert.Parameters["@cause"].Value = row.Cause;
        insert.Parameters["@movementType"].Value = row.MovementType;
        insert.Parameters["@subjectType"].Value = row.SubjectType;
        insert.Parameters["@subject"].Value = row.Subject;
        insert.Parameters["@amount"].Value = row.Amount;
        insert.Parameters["@payment"].Value = row.Payment;
        insert.Parameters["@notes"].Value = row.Notes;
        await insert.ExecuteNonQueryAsync();
    }
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

await using var verify = new MySqlCommand("SELECT Anno, COUNT(*) FROM MovCassa GROUP BY Anno ORDER BY Anno;", connection);
await using var verifyReader = await verify.ExecuteReaderAsync();
Console.WriteLine($"Backup: microfish_0001.{backupTable}");
Console.WriteLine($"Importati: {rows.Count} record; PuntoV impostato a 0.");
while (await verifyReader.ReadAsync()) Console.WriteLine($"Anno {verifyReader[0]}: {verifyReader[1]} record.");

static async Task<bool> ColumnExists(MySqlConnection connection, string name)
{
    const string sql = "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'MovCassa' AND COLUMN_NAME = @name;";
    await using var command = new MySqlCommand(sql, connection);
    command.Parameters.AddWithValue("@name", name);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
}

static async Task Execute(MySqlConnection connection, string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}

static int Number(string value) => value.StartsWith("V:") ? int.Parse(value[2..], CultureInfo.InvariantCulture) : 0;
static decimal Decimal(string value) => value.StartsWith("V:") ? decimal.Parse(value[2..], CultureInfo.InvariantCulture) : 0;
static DateTime Date(string value) => value.StartsWith("D:") ? DateTime.Parse(value[2..], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : throw new InvalidDataException("Data movimento nulla.");
static string Text(string value) => value.StartsWith("S:") ? Encoding.UTF8.GetString(Convert.FromBase64String(value[2..])).TrimEnd() : "";

internal sealed record LegacyRow(int Year, int Sector, int Code, DateTime MovementDate, int Cause, string MovementType, string SubjectType, int Subject, decimal Amount, int Payment, string Notes);
internal sealed class SecretsMarker;
