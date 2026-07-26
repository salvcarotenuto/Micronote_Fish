using System.Globalization;
using MySqlConnector;

var baseConnectionString = Environment.GetEnvironmentVariable("MICRONOTE_MIGRATION_CONNECTION")
    ?? throw new InvalidOperationException("Connessione di migrazione non configurata.");
var connectionBuilder = new MySqlConnectionStringBuilder(baseConnectionString) { Database = "Micronote_0001" };

if (args.Length > 0 && args[0] == "--swap")
{
    await SwapTablesAsync(connectionBuilder.ConnectionString);
    return;
}

var inputPath = args.Length > 0 ? Path.GetFullPath(args[0]) : throw new ArgumentException("File TSV mancante.");

var source = File.ReadLines(inputPath).Skip(1).Select(Parse).Where(row => row is not null).Cast<VatRow>().ToList();
var sales = source.Where(row => row.Sector == 20).ToList();
var nonSales = source.Where(row => row.Sector != 20).ToList();
var output = new List<OutputRow>(nonSales.Select(row => new OutputRow(
    row.Id, row.Year, row.Sector, row.Code, row.Store, row.VatRate, row.Taxable, row.Vat, 0)));

var anomalousSalesRows = 0;
var excludedSalesGroups = 0;
var mergedSalesGroups = 0;
foreach (var group in sales.GroupBy(row => new { row.Id, row.Year, row.Sector, row.Code, row.Store }))
{
    var taxable = group.Where(row => row.VatRate == 10m).ToList();
    var exempt = group.Where(row => row.VatRate == 0m).ToList();
    anomalousSalesRows += group.Count(row => row.VatRate != 0m && row.VatRate != 10m);
    if (taxable.Count == 0)
    {
        excludedSalesGroups++;
        continue;
    }

    output.Add(new OutputRow(group.Key.Id, group.Key.Year, group.Key.Sector, group.Key.Code, group.Key.Store, 10m,
        taxable.Sum(row => row.Taxable), taxable.Sum(row => row.Vat), exempt.Sum(row => row.Taxable + row.Vat)));
    mergedSalesGroups++;
}

await using var connection = new MySqlConnection(connectionBuilder.ConnectionString);
await connection.OpenAsync();
await using var transaction = await connection.BeginTransactionAsync();
try
{
    await ExecuteAsync(connection, transaction, "DROP TABLE IF EXISTS MovivaRg_New;");
    await ExecuteAsync(connection, transaction, """
        CREATE TABLE MovivaRg_New (
          ID int NOT NULL DEFAULT 0,
          Anno smallint DEFAULT 0,
          Settore tinyint DEFAULT 0,
          Codice int DEFAULT 0,
          PuntoV smallint DEFAULT 0,
          AliqIva decimal(5,2) DEFAULT 0.00,
          Imponibile decimal(12,2) DEFAULT 0.00,
          Iva decimal(12,2) DEFAULT 0.00,
          NonImpo decimal(12,2) DEFAULT 0.00
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """);

    const string insertSql = """
        INSERT INTO MovivaRg_New (ID, Anno, Settore, Codice, PuntoV, AliqIva, Imponibile, Iva, NonImpo)
        VALUES (@id, @year, @sector, @code, @store, @rate, @taxable, @vat, @exempt);
        """;
    await using var command = new MySqlCommand(insertSql, connection, transaction);
    command.Parameters.Add("@id", MySqlDbType.Int32);
    command.Parameters.Add("@year", MySqlDbType.Int16);
    command.Parameters.Add("@sector", MySqlDbType.Byte);
    command.Parameters.Add("@code", MySqlDbType.Int32);
    command.Parameters.Add("@store", MySqlDbType.Int16);
    command.Parameters.Add("@rate", MySqlDbType.Decimal);
    command.Parameters.Add("@taxable", MySqlDbType.Decimal);
    command.Parameters.Add("@vat", MySqlDbType.Decimal);
    command.Parameters.Add("@exempt", MySqlDbType.Decimal);
    foreach (var row in output)
    {
        command.Parameters["@id"].Value = row.Id;
        command.Parameters["@year"].Value = row.Year;
        command.Parameters["@sector"].Value = row.Sector;
        command.Parameters["@code"].Value = row.Code;
        command.Parameters["@store"].Value = row.Store;
        command.Parameters["@rate"].Value = row.VatRate;
        command.Parameters["@taxable"].Value = row.Taxable;
        command.Parameters["@vat"].Value = row.Vat;
        command.Parameters["@exempt"].Value = row.NonTaxable;
        await command.ExecuteNonQueryAsync();
    }
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

Console.WriteLine($"Sorgente: {source.Count}");
Console.WriteLine($"Non vendite preservate: {nonSales.Count}");
Console.WriteLine($"Gruppi vendite ricostruiti: {mergedSalesGroups}");
Console.WriteLine($"Gruppi vendite esclusi (senza 10%): {excludedSalesGroups}");
Console.WriteLine($"Righe vendite anomale 4%/22% ignorate: {anomalousSalesRows}");
Console.WriteLine($"MovivaRg_New inserite: {output.Count}");
Console.WriteLine($"Con NonImpo: {output.Count(row => row.NonTaxable != 0)}");
Console.WriteLine($"Totale NonImpo: {output.Sum(row => row.NonTaxable).ToString("N2", CultureInfo.GetCultureInfo("it-IT"))}");

static VatRow? Parse(string line)
{
    var fields = line.Split('\t');
    if (fields.Length != 8) return null;
    try
    {
        return new VatRow(Int(fields[0]), Short(fields[1]), Byte(fields[2]), Int(fields[3]), Short(fields[4]),
            Decimal(fields[5]), Decimal(fields[6]), Decimal(fields[7]));
    }
    catch { return null; }
}

static int Int(string value) => int.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);
static short Short(string value) => short.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);
static byte Byte(string value) => byte.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);
static decimal Decimal(string value) => decimal.Parse(string.IsNullOrWhiteSpace(value) ? "0" : value, NumberStyles.Any, CultureInfo.InvariantCulture);
static async Task ExecuteAsync(MySqlConnection connection, MySqlTransaction transaction, string sql)
{
    await using var command = new MySqlCommand(sql, connection, transaction);
    await command.ExecuteNonQueryAsync();
}

static async Task SwapTablesAsync(string connectionString)
{
    const string backupTable = "MovivaRg_Legacy_20260718";
    await using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync();

    static async Task<long> ScalarAsync(MySqlConnection connection, string sql)
    {
        await using var command = new MySqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    var newRows = await ScalarAsync(connection, "SELECT COUNT(*) FROM MovivaRg_New;");
    var hasNonTaxable = await ScalarAsync(connection, """
        SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'MovivaRg_New' AND COLUMN_NAME = 'NonImpo';
        """);
    var backupExists = await ScalarAsync(connection, $"""
        SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{backupTable}';
        """);
    if (newRows != 5714) throw new InvalidOperationException($"Conteggio staging inatteso: {newRows}.");
    if (hasNonTaxable != 1) throw new InvalidOperationException("Colonna NonImpo mancante nella staging.");
    if (backupExists != 0) throw new InvalidOperationException($"La tabella backup {backupTable} esiste già.");

    await using var command = new MySqlCommand(
        $"RENAME TABLE MovivaRg TO {backupTable}, MovivaRg_New TO MovivaRg;", connection);
    await command.ExecuteNonQueryAsync();
    Console.WriteLine($"Scambio completato. MovivaRg: {newRows} righe.");
    Console.WriteLine($"Backup precedente: {backupTable}.");
    Console.WriteLine($"Righe con NonImpo: {await ScalarAsync(connection, "SELECT COUNT(*) FROM MovivaRg WHERE NonImpo <> 0;")}");
}

internal sealed record VatRow(int Id, short Year, byte Sector, int Code, short Store, decimal VatRate, decimal Taxable, decimal Vat);
internal sealed record OutputRow(int Id, short Year, byte Sector, int Code, short Store, decimal VatRate, decimal Taxable, decimal Vat, decimal NonTaxable);
