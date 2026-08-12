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

var catalogNames = new List<string>();
await using (var command = new MySqlCommand("SELECT Nome FROM FixedTable ORDER BY Nome;", connection))
await using (var reader = await command.ExecuteReaderAsync())
    while (await reader.ReadAsync()) catalogNames.Add(reader.GetString(0));

var duplicates = new List<(string Original, string Template, int OriginalRows, int TemplateRows)>();
foreach (var name in catalogNames.Where(name => !name.Equals("FixedTable", StringComparison.OrdinalIgnoreCase)))
{
    var template = "Fixed_" + name;
    if (!await TableExistsAsync(name) || !await TableExistsAsync(template)) continue;
    duplicates.Add((name, template, await CountAsync(name), await CountAsync(template)));
}

Console.WriteLine($"Duplicati trovati: {duplicates.Count}");
foreach (var item in duplicates)
    Console.WriteLine($"{item.Original} ({item.OriginalRows}) -> {item.Template} ({item.TemplateRows})");

if (!apply) return;

foreach (var item in duplicates)
{
    await using var command = new MySqlCommand($"DROP TABLE `{item.Original.Replace("`", "``")}`;", connection);
    await command.ExecuteNonQueryAsync();
}

Console.WriteLine($"Tabelle originali eliminate: {duplicates.Count}");
Console.WriteLine($"Tabelle Fixed_* conservate con dati: {duplicates.Count}");

async Task<bool> TableExistsAsync(string table)
{
    await using var command = new MySqlCommand("""
        SELECT COUNT(*) FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = @database AND LOWER(TABLE_NAME) = LOWER(@table);
        """, connection);
    command.Parameters.AddWithValue("@database", masterDatabase);
    command.Parameters.AddWithValue("@table", table);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
}

async Task<int> CountAsync(string table)
{
    await using var command = new MySqlCommand($"SELECT COUNT(*) FROM `{table.Replace("`", "``")}`;", connection);
    return Convert.ToInt32(await command.ExecuteScalarAsync());
}

internal sealed class SecretsMarker;
