using System.Globalization;
using MySqlConnector;

if (args.Length != 2)
{
    Console.Error.WriteLine("Uso: ImportLegacyArticles <legacy.tsv> <database-mysql>");
    return 2;
}

var connectionString = Environment.GetEnvironmentVariable("MICRONOTE_DB_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Variabile MICRONOTE_DB_CONNECTION non configurata.");
    return 3;
}

var rows = File.ReadLines(args[0])
    .Skip(1)
    .Where(line => !string.IsNullOrWhiteSpace(line))
    .Select(ParseRow)
    .ToArray();

var duplicateCodes = rows
    .GroupBy(row => row.Code, StringComparer.OrdinalIgnoreCase)
    .Where(group => group.Count() > 1)
    .Select(group => group.Key)
    .ToArray();
if (duplicateCodes.Length > 0)
{
    Console.Error.WriteLine($"Codici legacy duplicati: {string.Join(", ", duplicateCodes)}");
    return 4;
}

var builder = new MySqlConnectionStringBuilder(connectionString)
{
    Database = args[1]
};

await using var connection = new MySqlConnection(builder.ConnectionString);
await connection.OpenAsync();
await using var transaction = await connection.BeginTransactionAsync();

await using (var countCommand = new MySqlCommand(
    "SELECT COUNT(*) FROM `Articoli`;",
    connection,
    transaction))
{
    var existingRows = Convert.ToInt64(await countCommand.ExecuteScalarAsync());
    if (existingRows != 0)
    {
        Console.Error.WriteLine(
            $"Importazione interrotta: Articoli contiene già {existingRows} record.");
        await transaction.RollbackAsync();
        return 5;
    }
}

const string sql = """
    INSERT INTO `Articoli`
        (`Codice`, `Descrizione`, `Categoria`, `Specie`, `Uma`, `Umv`,
         `PrezzoStd`, `Provenienza`, `Tara`, `GiacinC`, `GiacinP`,
         `CostoStd`, `PrIvato`, `AliqIva`)
    VALUES
        (@code, @description, @category, @species, @salesUnit, @purchaseUnit,
         @standardPrice, @origin, @tare, @initialPackages, @initialWeight,
         @standardCost, @vatIncludedPrice, @vatRate);
    """;

foreach (var row in rows)
{
    await using var command = new MySqlCommand(sql, connection, transaction);
    command.Parameters.AddWithValue("@code", row.Code);
    command.Parameters.AddWithValue("@description", row.Description);
    command.Parameters.AddWithValue("@category", row.Category);
    command.Parameters.AddWithValue("@species", row.Species);
    command.Parameters.AddWithValue("@salesUnit", row.SalesUnit);
    command.Parameters.AddWithValue("@purchaseUnit", row.PurchaseUnit);
    command.Parameters.AddWithValue("@standardPrice", row.StandardPrice);
    command.Parameters.AddWithValue("@origin", row.Origin);
    command.Parameters.AddWithValue("@tare", row.Tare);
    command.Parameters.AddWithValue("@initialPackages", row.InitialPackages);
    command.Parameters.AddWithValue("@initialWeight", row.InitialWeight);
    command.Parameters.AddWithValue("@standardCost", row.StandardCost);
    command.Parameters.AddWithValue("@vatIncludedPrice", row.VatIncludedPrice);
    command.Parameters.AddWithValue("@vatRate", row.VatRate);
    await command.ExecuteNonQueryAsync();
}

await transaction.CommitAsync();
Console.WriteLine($"Articoli importati: {rows.Length}");
return 0;

static LegacyArticle ParseRow(string line)
{
    var values = line.Split('\t');
    if (values.Length != 18)
    {
        throw new InvalidDataException($"Riga legacy con {values.Length} campi anziché 18.");
    }

    return new LegacyArticle(
        values[0].Trim(),
        values[1].Trim(),
        ParseInt(values[2]),
        ParseInt(values[3]),
        values[4].Trim(),
        values[5].Trim(),
        ParseDecimal(values[6]),
        ParseInt(values[7]),
        ParseDecimal(values[8]),
        ParseInt(values[9]),
        ParseDecimal(values[10]),
        ParseDecimal(values[13]),
        ParseDecimal(values[15]),
        ParseDecimal(values[17]));
}

static int ParseInt(string value) =>
    int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
        ? result
        : 0;

static decimal ParseDecimal(string value) =>
    decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
        ? result
        : 0;

internal sealed record LegacyArticle(
    string Code,
    string Description,
    int Category,
    int Species,
    string SalesUnit,
    string PurchaseUnit,
    decimal StandardPrice,
    int Origin,
    decimal Tare,
    int InitialPackages,
    decimal InitialWeight,
    decimal StandardCost,
    decimal VatIncludedPrice,
    decimal VatRate);
