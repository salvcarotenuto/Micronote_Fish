using System.Globalization;
using MySqlConnector;

var file = Path.GetFullPath(args[0]);
var activate = args.Any(a => string.Equals(a, "--activate", StringComparison.OrdinalIgnoreCase));
var baseCs = Environment.GetEnvironmentVariable("MICRONOTE_MIGRATION_CONNECTION") ?? throw new InvalidOperationException("Connessione mancante.");
var cs = new MySqlConnectionStringBuilder(baseCs) { Database = "Micronote_0001" }.ConnectionString;
var rows = File.ReadLines(file).Skip(1).Select(Parse).Where(x => x is not null).Cast<Row>().ToList();
await using var connection = new MySqlConnection(cs); await connection.OpenAsync();
var ids = new Dictionary<(short Year, int Code), int>();
await using (var command = new MySqlCommand("SELECT ID,Anno,Codice FROM Vendite", connection))
await using (var reader = await command.ExecuteReaderAsync())
    while (await reader.ReadAsync()) ids[(Convert.ToInt16(reader["Anno"]), Convert.ToInt32(reader["Codice"]))] = Convert.ToInt32(reader["ID"]);
var valid = rows.Where(r => ids.ContainsKey((r.Year, r.Code))).ToList();
var orphans = rows.Count - valid.Count;
await using var tx = await connection.BeginTransactionAsync();
try
{
    await Exec("DROP TABLE IF EXISTS VenditeRg_New");
    await Exec("""
      CREATE TABLE VenditeRg_New (
        ID int NOT NULL, Anno smallint NOT NULL DEFAULT 0, Codice int DEFAULT 0, PuntoV smallint DEFAULT 0,
        Imponibile decimal(12,2) DEFAULT 0.00, Iva decimal(12,2) DEFAULT 0.00, Totale decimal(12,2) DEFAULT 0.00,
        Contanti decimal(12,2) DEFAULT 0.00, Carta decimal(12,2) DEFAULT 0.00, Tickets decimal(12,2) DEFAULT 0.00,
        Assegni decimal(12,2) DEFAULT 0.00, Altro decimal(12,2) DEFAULT 0.00, Sospesi decimal(12,2) DEFAULT 0.00,
        Perdite decimal(12,2) DEFAULT 0.00
      ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
      """);
    const string sql = "INSERT INTO VenditeRg_New VALUES (@id,@year,@code,@store,@net,@vat,@total,@cash,@card,@tickets,@checks,@other,@suspended,@losses)";
    await using var command = new MySqlCommand(sql, connection, tx);
    foreach (var r in valid)
    {
        command.Parameters.Clear();
        command.Parameters.AddWithValue("@id", ids[(r.Year,r.Code)]); command.Parameters.AddWithValue("@year",r.Year);
        command.Parameters.AddWithValue("@code",r.Code); command.Parameters.AddWithValue("@store",r.Store);
        command.Parameters.AddWithValue("@net",r.Values[0]); command.Parameters.AddWithValue("@vat",r.Values[1]);
        command.Parameters.AddWithValue("@total",r.Values[2]); command.Parameters.AddWithValue("@cash",r.Values[3]);
        command.Parameters.AddWithValue("@card",r.Values[4]); command.Parameters.AddWithValue("@tickets",r.Values[5]);
        command.Parameters.AddWithValue("@checks",r.Values[6]); command.Parameters.AddWithValue("@other",r.Values[7]);
        command.Parameters.AddWithValue("@suspended",r.Values[8]); command.Parameters.AddWithValue("@losses",r.Values[9]);
        await command.ExecuteNonQueryAsync();
    }
    await tx.CommitAsync();
}
catch { await tx.RollbackAsync(); throw; }
Console.WriteLine($"Sorgente: {rows.Count}"); Console.WriteLine($"Inserite VenditeRg_New: {valid.Count}"); Console.WriteLine($"Orfane escluse: {orphans}");
await using (var check = new MySqlCommand("SELECT PuntoV,Imponibile,Iva,Totale,Contanti,Carta,Altro,Perdite FROM VenditeRg_New WHERE Anno=2025 AND Codice=361 ORDER BY PuntoV",connection))
await using (var reader = await check.ExecuteReaderAsync()) while(await reader.ReadAsync()) Console.WriteLine($"361 PV {reader["PuntoV"]}: netto={reader["Imponibile"]}, iva={reader["Iva"]}, totale={reader["Totale"]}, contanti={reader["Contanti"]}, carta={reader["Carta"]}, transitoria={reader["Altro"]}, perdite={reader["Perdite"]}");

if (activate)
{
    const string backup = "VenditeRg_Legacy_20260718";
    await using var activateCommand = new MySqlCommand($"DROP TABLE IF EXISTS {backup}; RENAME TABLE VenditeRg TO {backup}, VenditeRg_New TO VenditeRg;", connection);
    await activateCommand.ExecuteNonQueryAsync();
    Console.WriteLine($"Attivazione completata. Backup: {backup}");
}

async Task Exec(string sql) { await using var command = new MySqlCommand(sql,connection,tx); await command.ExecuteNonQueryAsync(); }
static Row? Parse(string line) { var f=line.Split('\t'); if(f.Length!=13)return null; try{return new Row(short.Parse(f[0]),int.Parse(f[1]),short.Parse(f[2]),f.Skip(3).Select(x=>decimal.Parse(string.IsNullOrWhiteSpace(x)?"0":x,NumberStyles.Any,CultureInfo.InvariantCulture)).ToArray());}catch{return null;} }
internal sealed record Row(short Year,int Code,short Store,decimal[] Values);
