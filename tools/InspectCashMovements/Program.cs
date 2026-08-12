using Microsoft.Extensions.Configuration;
using MySqlConnector;

var configuration = new ConfigurationBuilder().AddUserSecrets<SecretsMarker>(false).Build();
var configured = configuration.GetConnectionString("MicronoteServer")
    ?? configuration.GetConnectionString("MicronoteDb")
    ?? throw new InvalidOperationException("Connessione MySQL non configurata.");
var cs = new MySqlConnectionStringBuilder(configured) { Database = "MicroFish_Master" }.ConnectionString;
await using var connection = new MySqlConnection(cs);
await connection.OpenAsync();

const string databasesSql = """
    SELECT SCHEMA_NAME
    FROM information_schema.SCHEMATA
    WHERE LOWER(SCHEMA_NAME) = 'microfish_0001'
       OR LOWER(SCHEMA_NAME) LIKE 'microfish_0001_backup_%'
    ORDER BY CASE WHEN LOWER(SCHEMA_NAME) = 'microfish_0001' THEN 0 ELSE 1 END, SCHEMA_NAME DESC;
    """;
var databases = new List<string>();
await using (var command = new MySqlCommand(databasesSql, connection))
await using (var reader = await command.ExecuteReaderAsync())
    while (await reader.ReadAsync()) databases.Add(reader.GetString(0));

foreach (var database in databases)
{
    if (!database.Equals("microfish_0001", StringComparison.OrdinalIgnoreCase)) continue;
    var safe = "`" + database.Replace("`", "``") + "`";
    Console.WriteLine($"[{database}]");
    await using (var yearsCommand = new MySqlCommand(
        $"SELECT Anno, COUNT(*) AS Righe FROM {safe}.MovCassa GROUP BY Anno ORDER BY Anno;",
        connection))
    await using (var yearsReader = await yearsCommand.ExecuteReaderAsync())
    {
        Console.WriteLine("Righe per anno correnti:");
        while (await yearsReader.ReadAsync())
            Console.WriteLine($"{yearsReader["Anno"]}: {yearsReader["Righe"]}");
    }
    var count = 0;
    {
        await using var command = new MySqlCommand(
            $"SELECT ID, Anno, Settore, Codice, DataMov, Causale, CliFor, Ditta, Importo FROM {safe}.MovCassa WHERE Anno = 2026 ORDER BY ID;",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            Console.WriteLine($"ID={reader["ID"]}; Anno={reader["Anno"]}; Settore={reader["Settore"]}; Codice={reader["Codice"]}; Data={Convert.ToDateTime(reader["DataMov"]):yyyy-MM-dd}; Causale={reader["Causale"]}; CliFor={reader["CliFor"]}; Ditta={reader["Ditta"]}; Importo={reader["Importo"]}");
            count++;
        }
    }
    Console.WriteLine($"Totale 2026: {count}");

    const string listSql = """
        SELECT m.ID, m.Anno, m.Settore, m.Codice, m.DataMov,
               COALESCE(m.Causale, 0) AS Causale,
               COALESCE(cc.Descrizione, '') AS CausaleDescrizione,
               COALESCE(m.Annotazioni, '') AS MovimentoDescrizione,
               COALESCE(m.CliFor, '') AS CliFor,
               COALESCE(m.Ditta, 0) AS Ditta,
               CASE COALESCE(m.CliFor, '')
                 WHEN 'C' THEN COALESCE(c.Nome, '')
                 WHEN 'F' THEN COALESCE(f.Nome, '')
                 WHEN 'A' THEN COALESCE(a.Nome, '')
                 ELSE ''
               END AS DittaNome,
               COALESCE(m.PuntoV, 0) AS PuntoV,
               COALESCE(pv.Nome, '') AS PuntoVendita,
               CASE WHEN m.TipoMov = 'E' THEN COALESCE(m.Importo, 0) ELSE 0 END AS Entrata,
               CASE WHEN m.TipoMov = 'U' THEN COALESCE(m.Importo, 0) ELSE 0 END AS Uscita,
               COALESCE(m.ModoPag, 0) AS ModoPag
        FROM microfish_0001.MovCassa m
        LEFT JOIN microfish_0001.CausaliCassa cc ON cc.Codice = m.Causale
        LEFT JOIN microfish_0001.Clienti c ON m.CliFor = 'C' AND c.Codice = m.Ditta
        LEFT JOIN microfish_0001.Fornitori f ON m.CliFor = 'F' AND f.Codice = m.Ditta
        LEFT JOIN microfish_0001.Agenti a ON m.CliFor = 'A' AND a.Codice = m.Ditta
        LEFT JOIN microfish_0001.PuntiVendita pv ON pv.Codice = m.PuntoV
        WHERE m.Anno = 2026
        ORDER BY m.DataMov DESC, m.Codice DESC, m.ID DESC;
        """;
    await using var listCommand = new MySqlCommand(listSql, connection);
    await using var listReader = await listCommand.ExecuteReaderAsync();
    var listCount = 0;
    while (await listReader.ReadAsync()) listCount++;
    Console.WriteLine($"Totale query lista 2026: {listCount}");
}

internal sealed class SecretsMarker;
