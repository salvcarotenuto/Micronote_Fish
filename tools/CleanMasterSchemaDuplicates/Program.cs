using Microsoft.Extensions.Configuration;
using MySqlConnector;

const string masterDatabase = "MicroFish_Master";
var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var configuration = new ConfigurationBuilder().AddUserSecrets<SecretsMarker>(false).Build();
var configured = configuration.GetConnectionString("MicronoteServer")
    ?? configuration.GetConnectionString("MicronoteDb")
    ?? throw new InvalidOperationException("Connessione MySQL non configurata.");
var cs = new MySqlConnectionStringBuilder(configured) { Database = masterDatabase }.ConnectionString;

await using var connection = new MySqlConnection(cs);
await connection.OpenAsync();

var tables = new List<string>();
await using (var command = new MySqlCommand("""
    SELECT TABLE_NAME FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = @database ORDER BY TABLE_NAME;
    """, connection))
{
    command.Parameters.AddWithValue("@database", masterDatabase);
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
}

var duplicates = new List<(string Original, string Template, int OriginalRows, int TemplateRows)>();
foreach (var template in tables.Where(name => name.StartsWith("Schema_", StringComparison.OrdinalIgnoreCase)))
{
    var original = template["Schema_".Length..];
    if (!tables.Contains(original, StringComparer.OrdinalIgnoreCase)) continue;
    duplicates.Add((original, template, await CountAsync(original), await CountAsync(template)));
}

Console.WriteLine($"Duplicati aziendali trovati: {duplicates.Count}");
foreach (var item in duplicates)
    Console.WriteLine($"{item.Original} ({item.OriginalRows}) -> {item.Template} ({item.TemplateRows})");

if (!apply) return;

foreach (var item in duplicates)
{
    await using var command = new MySqlCommand($"DROP TABLE `{Q(item.Original)}`;", connection);
    await command.ExecuteNonQueryAsync();
}

Console.WriteLine($"Tabelle aziendali originali eliminate: {duplicates.Count}");
Console.WriteLine($"Tabelle Schema_* conservate: {duplicates.Count}");

async Task<int> CountAsync(string table)
{
    await using var command = new MySqlCommand($"SELECT COUNT(*) FROM `{Q(table)}`;", connection);
    return Convert.ToInt32(await command.ExecuteScalarAsync());
}

static string Q(string value) => value.Replace("`", "``");

internal sealed class SecretsMarker;
