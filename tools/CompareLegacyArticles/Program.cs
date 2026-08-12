using System.Globalization;
using MySqlConnector;

if (args.Length != 2)
{
    Console.Error.WriteLine("Uso: CompareLegacyArticles <legacy.tsv> <database-mysql>");
    return 2;
}

var connectionString = Environment.GetEnvironmentVariable("MICRONOTE_DB_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Variabile MICRONOTE_DB_CONNECTION non configurata.");
    return 3;
}

var legacy = File.ReadLines(args[0])
    .Skip(1)
    .Where(line => !string.IsNullOrWhiteSpace(line))
    .Select(ParseLegacy)
    .GroupBy(row => row.Code, StringComparer.OrdinalIgnoreCase)
    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

var mysql = new Dictionary<string, Article>(StringComparer.OrdinalIgnoreCase);
var builder = new MySqlConnectionStringBuilder(connectionString) { Database = args[1] };
await using (var connection = new MySqlConnection(builder.ConnectionString))
{
    await connection.OpenAsync();
    const string sql = """
        SELECT Codice, Descrizione, Categoria, Specie, Uma, Umv, PrezzoStd,
               Provenienza, Tara, GiacinC, GiacinP, CostoStd, PrIvato, AliqIva
        FROM Articoli
        ORDER BY Codice;
        """;
    await using var command = new MySqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var row = new Article(
            Text(reader, 0),
            Text(reader, 1),
            Number(reader, 2),
            Number(reader, 3),
            Text(reader, 4),
            Text(reader, 5),
            Number(reader, 6),
            Number(reader, 7),
            Number(reader, 8),
            Number(reader, 9),
            Number(reader, 10),
            Number(reader, 11),
            Number(reader, 12),
            Number(reader, 13));
        mysql[row.Code] = row;
    }
}

var onlyLegacy = legacy.Keys.Except(mysql.Keys, StringComparer.OrdinalIgnoreCase).Order().ToArray();
var onlyMysql = mysql.Keys.Except(legacy.Keys, StringComparer.OrdinalIgnoreCase).Order().ToArray();
var differences = new List<string>();

foreach (var code in legacy.Keys.Intersect(mysql.Keys, StringComparer.OrdinalIgnoreCase).Order())
{
    var left = legacy[code];
    var right = mysql[code];
    AddTextDifference(differences, code, "Descrizione", left.Description, right.Description);
    AddNumberDifference(differences, code, "Categoria", left.Category, right.Category);
    AddNumberDifference(differences, code, "Specie", left.Species, right.Species);
    AddTextDifference(differences, code, "Ums", left.SalesUnit, right.SalesUnit);
    AddTextDifference(differences, code, "Umv", left.PurchaseUnit, right.PurchaseUnit);
    AddNumberDifference(differences, code, "PrezzoStd", left.StandardPrice, right.StandardPrice);
    AddNumberDifference(differences, code, "Provenienza", left.Origin, right.Origin);
    AddNumberDifference(differences, code, "Tara", left.Tare, right.Tare);
    AddNumberDifference(differences, code, "GiacinC", left.InitialPackages, right.InitialPackages);
    AddNumberDifference(differences, code, "GiacinP", left.InitialWeight, right.InitialWeight);
    AddNumberDifference(differences, code, "CostoStd", left.StandardCost, right.StandardCost);
    AddNumberDifference(differences, code, "PrIvato", left.VatIncludedPrice, right.VatIncludedPrice);
    AddNumberDifference(differences, code, "AliqIva", left.VatRate, right.VatRate);
}

Console.WriteLine($"Legacy: {legacy.Count}");
Console.WriteLine($"MySQL: {mysql.Count}");
Console.WriteLine($"Solo legacy: {onlyLegacy.Length}");
Console.WriteLine($"Solo MySQL: {onlyMysql.Length}");
Console.WriteLine($"Differenze di campo: {differences.Count}");
PrintCodes("Codici solo legacy", onlyLegacy);
PrintCodes("Codici solo MySQL", onlyMysql);
foreach (var difference in differences)
{
    Console.WriteLine(difference);
}

return differences.Count == 0 && onlyLegacy.Length == 0 && onlyMysql.Length == 0 ? 0 : 1;

static Article ParseLegacy(string line)
{
    var values = line.Split('\t');
    if (values.Length != 18)
    {
        throw new InvalidDataException($"Riga legacy con {values.Length} campi anziché 18.");
    }

    return new Article(
        values[0].Trim(),
        values[1].Trim(),
        ParseNumber(values[2]),
        ParseNumber(values[3]),
        values[4].Trim(),
        values[5].Trim(),
        ParseNumber(values[6]),
        ParseNumber(values[7]),
        ParseNumber(values[8]),
        ParseNumber(values[9]),
        ParseNumber(values[10]),
        ParseNumber(values[13]),
        ParseNumber(values[15]),
        ParseNumber(values[17]));
}

static decimal ParseNumber(string value) =>
    decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
        ? result
        : 0;

static string Text(MySqlDataReader reader, int ordinal) =>
    reader.IsDBNull(ordinal) ? "" : Convert.ToString(reader.GetValue(ordinal))?.Trim() ?? "";

static decimal Number(MySqlDataReader reader, int ordinal) =>
    reader.IsDBNull(ordinal) ? 0 : Convert.ToDecimal(reader.GetValue(ordinal));

static void AddTextDifference(
    List<string> differences, string code, string field, string legacy, string mysql)
{
    if (!string.Equals(legacy.Trim(), mysql.Trim(), StringComparison.Ordinal))
    {
        differences.Add($"{code}\t{field}\tlegacy=[{legacy}]\tmysql=[{mysql}]");
    }
}

static void AddNumberDifference(
    List<string> differences, string code, string field, decimal legacy, decimal mysql)
{
    if (Math.Abs(legacy - mysql) > 0.0001m)
    {
        differences.Add(
            $"{code}\t{field}\tlegacy={legacy.ToString(CultureInfo.InvariantCulture)}" +
            $"\tmysql={mysql.ToString(CultureInfo.InvariantCulture)}");
    }
}

static void PrintCodes(string label, string[] codes)
{
    if (codes.Length > 0)
    {
        Console.WriteLine($"{label}: {string.Join(", ", codes)}");
    }
}

internal sealed record Article(
    string Code,
    string Description,
    decimal Category,
    decimal Species,
    string SalesUnit,
    string PurchaseUnit,
    decimal StandardPrice,
    decimal Origin,
    decimal Tare,
    decimal InitialPackages,
    decimal InitialWeight,
    decimal StandardCost,
    decimal VatIncludedPrice,
    decimal VatRate);
