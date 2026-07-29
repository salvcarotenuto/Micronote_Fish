using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

if (args.Length != 2)
{
    Console.Error.WriteLine("Uso: MigrateLegacySales <cartella-tsv> <database-mysql>");
    return 2;
}

var source = Path.GetFullPath(args[0]);
var databaseName = args[1];
if (databaseName != "MicroFish_0001")
    throw new InvalidOperationException("Destinazione ammessa: MicroFish_0001.");

var headers = File.ReadLines(Path.Combine(source, "vendite.tsv")).Select(ParseHeader).ToArray();
var rows = File.ReadLines(Path.Combine(source, "venditerg.tsv")).Select(ParseRow).ToArray();
var invalidSector = headers.FirstOrDefault(x => x.Sector != 20)
    ?? throwIfRowsHaveInvalidSector(rows);
if (invalidSector is not null)
    throw new InvalidOperationException($"Settore legacy inatteso: {invalidSector.Sector}.");
Unique(headers.Select(x => (x.Year, x.Code)), "Vendite");
Unique(rows.Select(x => (x.Year, x.Code, x.RowNumber)), "VenditeRg");
var keys = headers.Select(x => (x.Year, x.Code)).ToHashSet();
var orphan = rows.FirstOrDefault(x => !keys.Contains((x.Year, x.Code)));
if (orphan is not null)
    throw new InvalidOperationException($"Riga legacy orfana: {orphan.Year}/{orphan.Sector}/{orphan.Code}/{orphan.RowNumber}.");

var configuration = new ConfigurationBuilder().AddUserSecrets<SecretsMarker>(false).Build();
var baseCs = configuration.GetConnectionString("MicronoteServer")
    ?? configuration.GetConnectionString("MicronoteDb")
    ?? throw new InvalidOperationException("Connessione MySQL non configurata.");
var cs = new MySqlConnectionStringBuilder(baseCs) { Database = databaseName, DefaultCommandTimeout = 600 }.ConnectionString;
await using var connection = new MySqlConnection(cs);
await connection.OpenAsync();
Console.WriteLine($"Origine: {headers.Length} vendite, {rows.Length} righe.");
Console.WriteLine($"Destinazione verificata: {connection.Database}.");

var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
var backupH = $"Vendite_PreLegacy_{stamp}";
var backupR = $"VenditeRg_PreLegacy_{stamp}";
await Exec($"CREATE TABLE `{backupH}` LIKE `Vendite`; INSERT INTO `{backupH}` SELECT * FROM `Vendite`;");
await Exec($"CREATE TABLE `{backupR}` LIKE `VenditeRg`; INSERT INTO `{backupR}` SELECT * FROM `VenditeRg`;");
Console.WriteLine($"Backup: {backupH}, {backupR}.");

await Exec("SET FOREIGN_KEY_CHECKS=0;");
try
{
    await Exec("""
        DROP TABLE `VenditeRg`;
        DROP TABLE `Vendite`;
        CREATE TABLE `Vendite` (
          `ID` INT NOT NULL AUTO_INCREMENT,
          `Anno` SMALLINT NOT NULL DEFAULT 0,
          `Codice` INT NOT NULL DEFAULT 0,
          `Stato` TINYINT NOT NULL DEFAULT 0,
          `NumDoc` INT NOT NULL DEFAULT 0,
          `DataDoc` DATE NULL,
          `Cliente` INT NOT NULL DEFAULT 0,
          `Merce` DECIMAL(12,2) NOT NULL DEFAULT 0,
          `Agente` SMALLINT NOT NULL DEFAULT 0,
          `Provvigione` DECIMAL(5,2) NOT NULL DEFAULT 0,
          `Iva` DECIMAL(10,2) NOT NULL DEFAULT 0,
          `Totale` DECIMAL(12,2) NOT NULL DEFAULT 0,
          `Abbuono` DECIMAL(10,2) NOT NULL DEFAULT 0,
          `Pagato` DECIMAL(12,2) NOT NULL DEFAULT 0,
          `PuntoV` SMALLINT NOT NULL DEFAULT 0,
          PRIMARY KEY (`ID`),
          UNIQUE KEY `UX_Vendite_Anno_Codice` (`Anno`,`Codice`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        CREATE TABLE `VenditeRg` (
          `ID` INT NOT NULL,
          `Riga` SMALLINT NOT NULL DEFAULT 0,
          `Articolo` VARCHAR(30) NOT NULL DEFAULT '',
          `Ums` VARCHAR(10) NOT NULL DEFAULT '',
          `Colli` SMALLINT NOT NULL DEFAULT 0,
          `Tara` DECIMAL(10,3) NOT NULL DEFAULT 0,
          `Quantita` DECIMAL(10,3) NOT NULL DEFAULT 0,
          `Prezzo` DECIMAL(10,3) NOT NULL DEFAULT 0,
          `Iva` DECIMAL(5,2) NOT NULL DEFAULT 0,
          `Importo` DECIMAL(12,2) NOT NULL DEFAULT 0,
          PRIMARY KEY (`ID`,`Riga`),
          CONSTRAINT `FK_VenditeRg_Vendite` FOREIGN KEY (`ID`) REFERENCES `Vendite` (`ID`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """);
}
finally { await Exec("SET FOREIGN_KEY_CHECKS=1;"); }

await using var transaction = await connection.BeginTransactionAsync();
try
{
    var ids = new Dictionary<(int, int), int>();
    foreach (var h in headers)
    {
        await using var command = new MySqlCommand("""
            INSERT INTO Vendite
            (Anno,Codice,Stato,NumDoc,DataDoc,Cliente,Merce,Agente,
             Provvigione,Iva,Totale,Abbuono,Pagato,PuntoV)
            VALUES
            (@y,@c,@state,@number,@date,@customer,@goods,@agent,
             @commission,@vat,@total,@discount,@paid,@store);
            """, connection, transaction);
        AddHeaderParameters(command, h);
        await command.ExecuteNonQueryAsync();
        ids[(h.Year, h.Code)] = checked((int)command.LastInsertedId);
    }
    foreach (var r in rows)
    {
        await using var command = new MySqlCommand("""
            INSERT INTO VenditeRg
            (ID,Riga,Articolo,Ums,
             Colli,Tara,Quantita,Prezzo,Iva,Importo)
            VALUES
            (@id,@row,@article,@unit,
             @packages,@tare,@netWeight,@price,@vat,@amount);
            """, connection, transaction);
        AddRowParameters(command, r, ids[(r.Year, r.Code)]);
        await command.ExecuteNonQueryAsync();
    }
    await transaction.CommitAsync();
}
catch { await transaction.RollbackAsync(); throw; }

var targetHeaders = await Scalar("SELECT COUNT(*) FROM Vendite;");
var targetRows = await Scalar("SELECT COUNT(*) FROM VenditeRg;");
var orphans = await Scalar("SELECT COUNT(*) FROM VenditeRg r LEFT JOIN Vendite v ON v.ID=r.ID WHERE v.ID IS NULL;");
var mismatches = await Scalar("SELECT COUNT(*) FROM VenditeRg r LEFT JOIN Vendite v ON v.ID=r.ID WHERE v.ID IS NULL;");
if (targetHeaders != headers.Length || targetRows != rows.Length || orphans != 0 || mismatches != 0)
    throw new InvalidOperationException($"Verifica fallita: testate={targetHeaders}, righe={targetRows}, orfane={orphans}, ID discordanti={mismatches}.");
Console.WriteLine($"Verifica completata: {targetHeaders} testate, {targetRows} righe, 0 orfane, 0 ID discordanti.");
Console.WriteLine("MIGRAZIONE VENDITE COMPLETATA.");
return 0;

static Header ParseHeader(string l) { var f=l.Split('\t'); if(f.Length!=16) throw new InvalidDataException(); return new(I(f[0]),I(f[1]),I(f[2]),I(f[3]),I(f[4]),D(f[5]),I(f[6]),M(f[7],2),I(f[8]),M(f[9],2),M(f[10],2),M(f[11],2),M(f[12],2),M(f[13],2),M(f[14],2),I(f[15])); }
static DetailRow ParseRow(string l) { var f=l.Split('\t'); if(f.Length!=20) throw new InvalidDataException(); return new(I(f[0]),I(f[1]),I(f[2]),I(f[3]),I(f[4]),D(f[5]),T(f[6]),I(f[7]),I(f[8]),T(f[9]),M(f[10],3),M(f[11],3),M(f[12],3),M(f[13],3),M(f[14],3),M(f[15],2),M(f[16],2),M(f[17],3),M(f[18],3),M(f[19],2)); }
static int I(string x)=>int.Parse(x,CultureInfo.InvariantCulture);
static decimal M(string x,int d)=>Math.Round(decimal.Parse(x,NumberStyles.Float,CultureInfo.InvariantCulture),d,MidpointRounding.AwayFromZero);
static DateOnly? D(string x)=>x=="0"||x==""?null:DateOnly.ParseExact(x,"yyyy-MM-dd",CultureInfo.InvariantCulture);
static string T(string x)=>Encoding.UTF8.GetString(Convert.FromBase64String(x));
static void Unique<T>(IEnumerable<T> values,string label) where T:notnull { var d=values.GroupBy(x=>x).FirstOrDefault(g=>g.Count()>1); if(d!=null) throw new InvalidOperationException($"{label}: chiave duplicata {d.Key}."); }
static Header? throwIfRowsHaveInvalidSector(IEnumerable<DetailRow> rows) { var row=rows.FirstOrDefault(x=>x.Sector!=20); return row is null?null:new Header(row.Year,row.Sector,row.Code,0,0,null,0,0,0,0,0,0,0,0,0,0); }
static void AddHeaderParameters(MySqlCommand c,Header h) { c.Parameters.AddWithValue("@y",h.Year);c.Parameters.AddWithValue("@c",h.Code);c.Parameters.AddWithValue("@state",h.State);c.Parameters.AddWithValue("@number",h.Number);c.Parameters.AddWithValue("@date",h.Date?.ToDateTime(TimeOnly.MinValue)??(object)DBNull.Value);c.Parameters.AddWithValue("@customer",h.Customer);c.Parameters.AddWithValue("@goods",h.Goods);c.Parameters.AddWithValue("@agent",h.Agent);c.Parameters.AddWithValue("@commission",h.Commission);c.Parameters.AddWithValue("@vat",h.Vat);c.Parameters.AddWithValue("@total",h.Total);c.Parameters.AddWithValue("@discount",h.Discount);c.Parameters.AddWithValue("@paid",h.Paid);c.Parameters.AddWithValue("@store",h.Store); }
static void AddRowParameters(MySqlCommand c,DetailRow r,int id) { c.Parameters.AddWithValue("@id",id);c.Parameters.AddWithValue("@row",r.RowNumber);c.Parameters.AddWithValue("@article",r.Article);c.Parameters.AddWithValue("@unit",r.Unit);c.Parameters.AddWithValue("@packages",r.Packages);c.Parameters.AddWithValue("@tare",r.Tare);c.Parameters.AddWithValue("@netWeight",r.NetWeight);c.Parameters.AddWithValue("@price",r.Price);c.Parameters.AddWithValue("@vat",r.Vat);c.Parameters.AddWithValue("@amount",r.Amount); }
async Task Exec(string sql) { await using var c=new MySqlCommand(sql,connection); await c.ExecuteNonQueryAsync(); }
async Task<long> Scalar(string sql) { await using var c=new MySqlCommand(sql,connection); return Convert.ToInt64(await c.ExecuteScalarAsync()); }
internal sealed class SecretsMarker;
internal sealed record Header(int Year,int Sector,int Code,int State,int Number,DateOnly? Date,int Customer,decimal Goods,int Agent,decimal Settled,decimal Commission,decimal Vat,decimal Total,decimal Paid,decimal Discount,int Store);
internal sealed record DetailRow(int Year,int Sector,int Code,int RowNumber,int Customer,DateOnly? Date,string Article,int Supplier,int Warehouse,string Unit,decimal Packages,decimal Gross,decimal Tare,decimal NetWeight,decimal Price,decimal Discount,decimal Vat,decimal NetPrice,decimal VatPrice,decimal Amount);
