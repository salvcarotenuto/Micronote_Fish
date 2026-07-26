using MySqlConnector;

const string connectionEnvName = "MICRONOTE_DB_CONNECTION";

var options = ParseArgs(args);
if (options.ShowHelp)
{
    PrintHelp();
    return;
}

var connectionString = Environment.GetEnvironmentVariable(connectionEnvName);
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine($"Variabile ambiente {connectionEnvName} non valorizzata.");
    Console.Error.WriteLine("Usare tools\\DbSchema.ps1, oppure impostare la variabile e rieseguire il tool.");
    Environment.ExitCode = 2;
    return;
}

if (!string.IsNullOrWhiteSpace(options.TargetDatabase))
{
    connectionString = new MySqlConnectionStringBuilder(connectionString)
    {
        Database = options.TargetDatabase
    }.ConnectionString;
}

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();
Console.WriteLine($"Database: {connection.Database}");

if (options.ListTables)
{
    await ListTablesAsync(connection);
    return;
}

if (options.SalesAudit)
{
    await SalesAuditAsync(connection);
    return;
}

if (options.PurchaseAudit)
{
    await PurchaseAuditAsync(connection);
    return;
}

if (string.IsNullOrWhiteSpace(options.TableName))
{
    PrintHelp();
    Environment.ExitCode = 2;
    return;
}

if (options.ShowCreate)
{
    await ShowCreateTableAsync(connection, options.TableName);
    return;
}

if (options.ShowIndexes)
{
    await ShowIndexesAsync(connection, options.TableName);
    return;
}

await ShowColumnsAsync(connection, options.TableName);

static DbSchemaOptions ParseArgs(string[] args)
{
    var options = new DbSchemaOptions();
    foreach (var arg in args)
    {
        if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("-h", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("/?", StringComparison.OrdinalIgnoreCase))
        {
            options.ShowHelp = true;
        }
        else if (arg.Equals("--list", StringComparison.OrdinalIgnoreCase)
                 || arg.Equals("-l", StringComparison.OrdinalIgnoreCase))
        {
            options.ListTables = true;
        }
        else if (arg.Equals("--create", StringComparison.OrdinalIgnoreCase)
                 || arg.Equals("-c", StringComparison.OrdinalIgnoreCase))
        {
            options.ShowCreate = true;
        }
        else if (arg.Equals("--indexes", StringComparison.OrdinalIgnoreCase)
                 || arg.Equals("-i", StringComparison.OrdinalIgnoreCase))
        {
            options.ShowIndexes = true;
        }
        else if (arg.Equals("--sales-audit", StringComparison.OrdinalIgnoreCase))
        {
            options.SalesAudit = true;
        }
        else if (arg.Equals("--purchase-audit", StringComparison.OrdinalIgnoreCase))
        {
            options.PurchaseAudit = true;
        }
        else if (arg.StartsWith("--database=", StringComparison.OrdinalIgnoreCase))
        {
            options.TargetDatabase = arg["--database=".Length..].Trim();
        }
        else if (string.IsNullOrWhiteSpace(options.TableName))
        {
            options.TableName = arg;
        }
    }

    return options;
}

static async Task ListTablesAsync(MySqlConnection connection)
{
    await using var command = new MySqlCommand(
        """
        SELECT TABLE_NAME
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = DATABASE()
        ORDER BY TABLE_NAME;
        """,
        connection);

    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        Console.WriteLine(reader.GetString("TABLE_NAME"));
    }
}

static async Task ShowColumnsAsync(MySqlConnection connection, string tableName)
{
    await using var command = new MySqlCommand(
        """
        SELECT COLUMN_NAME,
               DATA_TYPE,
               COLUMN_TYPE,
               IS_NULLABLE,
               COALESCE(COLUMN_DEFAULT, '') AS COLUMN_DEFAULT,
               COALESCE(EXTRA, '') AS EXTRA,
               COALESCE(COLUMN_KEY, '') AS COLUMN_KEY
        FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND LOWER(TABLE_NAME) = LOWER(@tableName)
        ORDER BY ORDINAL_POSITION;
        """,
        connection);
    command.Parameters.AddWithValue("@tableName", tableName);

    await using var reader = await command.ExecuteReaderAsync();
    var rows = new List<string[]>();
    while (await reader.ReadAsync())
    {
        rows.Add([
            reader.GetString("COLUMN_NAME"),
            reader.GetString("DATA_TYPE"),
            reader.GetString("COLUMN_TYPE"),
            reader.GetString("IS_NULLABLE"),
            reader.GetString("COLUMN_DEFAULT"),
            reader.GetString("EXTRA"),
            reader.GetString("COLUMN_KEY")
        ]);
    }

    if (rows.Count == 0)
    {
        Console.Error.WriteLine($"Tabella non trovata: {tableName}");
        Environment.ExitCode = 1;
        return;
    }

    PrintTable(["Colonna", "Tipo", "Definizione", "Null", "Default", "Extra", "Key"], rows);
}

static async Task SalesAuditAsync(MySqlConnection connection)
{
    const string sql = """
        WITH iva AS (
            SELECT Anno, Codice,
                   SUM(CASE WHEN COALESCE(AliqIva, 0) = 0 THEN COALESCE(Imponibile, 0) ELSE 0 END) AS NonImponibile,
                   SUM(CASE WHEN COALESCE(AliqIva, 0) <> 0 THEN COALESCE(Imponibile, 0) ELSE 0 END) AS Imponibile,
                   SUM(COALESCE(Iva, 0)) AS Iva,
                   COUNT(*) AS Righe
            FROM MovIvaRg
            WHERE Settore = 20
            GROUP BY Anno, Codice
        ),
        punti AS (
            SELECT Anno, Codice,
                   COUNT(DISTINCT PuntoV) AS NumeroPunti,
                   SUM(COALESCE(Totale, 0)) AS TotaleRighe,
                   SUM(COALESCE(Altro, 0)) AS AltroRighe,
                   SUM(CASE WHEN COALESCE(Totale, 0) <> 0 THEN 1 ELSE 0 END) AS RigheConTotale
            FROM VenditeRg
            GROUP BY Anno, Codice
        ),
        testate_iva AS (
            SELECT Anno, Codice, COUNT(*) AS NumeroTestate
            FROM MovIva
            WHERE Settore = 20
            GROUP BY Anno, Codice
        ),
        verifica AS (
            SELECT v.Anno, v.Codice,
                   COALESCE(v.Totale, 0) AS TotaleVendite,
                   COALESCE(v.Altro, 0) AS NonImponibileAtteso,
                   ROUND((COALESCE(v.Totale, 0) - COALESCE(v.Altro, 0)) / 1.10, 2) AS ImponibileAtteso,
                   (COALESCE(v.Totale, 0) - COALESCE(v.Altro, 0))
                     - ROUND((COALESCE(v.Totale, 0) - COALESCE(v.Altro, 0)) / 1.10, 2) AS IvaAttesa,
                   COALESCE(i.NonImponibile, 0) AS NonImponibileIva,
                   COALESCE(i.Imponibile, 0) AS ImponibileIva,
                   COALESCE(i.Iva, 0) AS IvaIva,
                   COALESCE(i.Righe, 0) AS RigheIva,
                   COALESCE(p.NumeroPunti, 0) AS NumeroPunti,
                   COALESCE(p.TotaleRighe, 0) AS TotaleRighe,
                   COALESCE(p.AltroRighe, 0) AS AltroRighe,
                   COALESCE(p.RigheConTotale, 0) AS RigheConTotale,
                   COALESCE(t.NumeroTestate, 0) AS NumeroTestate
            FROM Vendite v
            LEFT JOIN iva i ON i.Anno = v.Anno AND i.Codice = v.Codice
            LEFT JOIN punti p ON p.Anno = v.Anno AND p.Codice = v.Codice
            LEFT JOIN testate_iva t ON t.Anno = v.Anno AND t.Codice = v.Codice
        )
        SELECT COUNT(*) AS Vendite,
               SUM(CASE WHEN TotaleVendite <> 0 THEN 1 ELSE 0 END) AS ConTotale,
               SUM(CASE WHEN RigheIva = 0 THEN 1 ELSE 0 END) AS SenzaRigheIva,
               SUM(CASE WHEN NumeroPunti = 1 THEN 1 ELSE 0 END) AS SingoloPunto,
               SUM(CASE WHEN NumeroPunti > 1 THEN 1 ELSE 0 END) AS Multipunto,
               SUM(CASE WHEN NumeroPunti = 0 THEN 1 ELSE 0 END) AS SenzaPunto,
               SUM(CASE WHEN ABS(TotaleVendite - TotaleRighe) <= 0.02
                             AND ABS(NonImponibileAtteso - AltroRighe) <= 0.02
                        THEN 1 ELSE 0 END) AS RigheCoerentiConTestata,
               SUM(CASE WHEN RigheConTotale = NumeroPunti AND NumeroPunti > 0
                        THEN 1 ELSE 0 END) AS TuttiIPuntiConTotale,
               SUM(CASE WHEN NumeroTestate = 0 THEN 1 ELSE 0 END) AS SenzaTestataMovIva,
               SUM(CASE WHEN NumeroTestate = 1 THEN 1 ELSE 0 END) AS TestataMovIvaUnivoca,
               SUM(CASE WHEN NumeroTestate > 1 THEN 1 ELSE 0 END) AS TestateMovIvaDuplicate,
               SUM(CASE WHEN ABS(NonImponibileAtteso - NonImponibileIva) <= 0.02
                             AND ABS(ImponibileAtteso - ImponibileIva) <= 0.02
                             AND ABS(IvaAttesa - IvaIva) <= 0.02
                        THEN 1 ELSE 0 END) AS Quadrate,
               SUM(CASE WHEN ABS(NonImponibileAtteso - NonImponibileIva) > 0.02
                             OR ABS(ImponibileAtteso - ImponibileIva) > 0.02
                             OR ABS(IvaAttesa - IvaIva) > 0.02
                        THEN 1 ELSE 0 END) AS DaRiallineare,
               ROUND(SUM(TotaleVendite), 2) AS TotaleVendite,
               ROUND(SUM(NonImponibileAtteso), 2) AS NonImponibileAtteso,
               ROUND(SUM(ImponibileAtteso), 2) AS ImponibileAtteso,
               ROUND(SUM(IvaAttesa), 2) AS IvaAttesa,
               ROUND(SUM(NonImponibileIva), 2) AS NonImponibileMovIva,
               ROUND(SUM(ImponibileIva), 2) AS ImponibileMovIva,
               ROUND(SUM(IvaIva), 2) AS IvaMovIva
        FROM verifica;

        SELECT Anno,
               COUNT(DISTINCT Codice) AS Vendite,
               ROUND(SUM(CASE WHEN COALESCE(AliqIva, 0) <> 0 THEN COALESCE(Imponibile, 0) ELSE 0 END), 2) AS Imponibile,
               ROUND(SUM(CASE WHEN COALESCE(AliqIva, 0) = 0 THEN COALESCE(Imponibile, 0) ELSE 0 END), 2) AS NonImponibile,
               ROUND(SUM(COALESCE(Iva, 0)), 2) AS Iva
        FROM MovIvaRg
        WHERE Settore = 20
        GROUP BY Anno
        ORDER BY Anno DESC;

        SELECT v.Anno,
               COUNT(*) AS Vendite,
               SUM(CASE WHEN miv.ID IS NULL THEN 1 ELSE 0 END) AS SenzaTestata,
               SUM(CASE WHEN iva.ID IS NULL THEN 1 ELSE 0 END) AS SenzaDettaglio,
               ROUND(SUM(COALESCE(iva.Imponibile, 0)), 2) AS Imponibile,
               ROUND(SUM(COALESCE(iva.NonImponibile, 0)), 2) AS NonImponibile,
               ROUND(SUM(COALESCE(iva.Iva, 0)), 2) AS Iva
        FROM Vendite v
        LEFT JOIN MovIva miv ON miv.Anno = v.Anno
                            AND miv.Codice = v.Codice
                            AND miv.Settore = 20
        LEFT JOIN (
            SELECT ID,
                   SUM(CASE WHEN COALESCE(AliqIva, 0) <> 0 THEN COALESCE(Imponibile, 0) ELSE 0 END) AS Imponibile,
                   SUM(CASE WHEN COALESCE(AliqIva, 0) = 0 THEN COALESCE(Imponibile, 0) ELSE 0 END) AS NonImponibile,
                   SUM(COALESCE(Iva, 0)) AS Iva
            FROM MovIvaRg
            WHERE Settore = 20
            GROUP BY ID
        ) iva ON iva.ID = miv.ID
        GROUP BY v.Anno
        ORDER BY v.Anno DESC;
        """;

    await using var command = new MySqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        return;
    }

    for (var index = 0; index < reader.FieldCount; index++)
    {
        Console.WriteLine($"{reader.GetName(index)}: {Convert.ToString(reader.GetValue(index))}");
    }

    if (await reader.NextResultAsync())
    {
        while (await reader.ReadAsync())
        {
            Console.WriteLine(
                $"Anno {reader["Anno"]}: {reader["Vendite"]} vendite; " +
                $"imponibile {reader["Imponibile"]}; non imponibile {reader["NonImponibile"]}; IVA {reader["Iva"]}");
        }
    }

    if (await reader.NextResultAsync())
    {
        while (await reader.ReadAsync())
        {
            Console.WriteLine(
                $"Join ID anno {reader["Anno"]}: {reader["Vendite"]} vendite; " +
                $"senza testata {reader["SenzaTestata"]}; senza dettaglio {reader["SenzaDettaglio"]}; " +
                $"imponibile {reader["Imponibile"]}; non imponibile {reader["NonImponibile"]}; IVA {reader["Iva"]}");
        }
    }
}

static async Task PurchaseAuditAsync(MySqlConnection connection)
{
    const string sql = """
        WITH righe AS (
            SELECT ID,
                   SUM(COALESCE(Imponibile, 0)) AS Imponibile,
                   SUM(COALESCE(Iva, 0)) AS Iva,
                   COUNT(*) AS NumeroRighe
            FROM MovIvaRg
            WHERE Settore = 10
            GROUP BY ID
        ),
        verifica AS (
            SELECT mi.ID,
                   COALESCE(mi.Imponibile, 0) AS ImponibileTestata,
                   COALESCE(mi.Iva, 0) AS IvaTestata,
                   COALESCE(mi.Totale, 0) AS TotaleTestata,
                   COALESCE(r.Imponibile, 0) AS ImponibileRighe,
                   COALESCE(r.Iva, 0) AS IvaRighe,
                   COALESCE(r.NumeroRighe, 0) AS NumeroRighe
            FROM MovIva mi
            LEFT JOIN righe r ON r.ID = mi.ID
            WHERE mi.Settore = 10
        )
        SELECT COUNT(*) AS Fatture,
               SUM(CASE WHEN TotaleTestata <> 0 THEN 1 ELSE 0 END) AS ConTotale,
               SUM(CASE WHEN NumeroRighe = 0 THEN 1 ELSE 0 END) AS SenzaRigheIva,
               SUM(CASE WHEN ABS(ImponibileTestata - ImponibileRighe) <= 0.02
                             AND ABS(IvaTestata - IvaRighe) <= 0.02
                             AND ABS(TotaleTestata - ImponibileRighe - IvaRighe) <= 0.02
                        THEN 1 ELSE 0 END) AS Quadrate,
               SUM(CASE WHEN ABS(ImponibileTestata - ImponibileRighe) > 0.02
                             OR ABS(IvaTestata - IvaRighe) > 0.02
                             OR ABS(TotaleTestata - ImponibileRighe - IvaRighe) > 0.02
                        THEN 1 ELSE 0 END) AS DaVerificare,
               ROUND(SUM(ImponibileTestata), 2) AS ImponibileTestata,
               ROUND(SUM(IvaTestata), 2) AS IvaTestata,
               ROUND(SUM(TotaleTestata), 2) AS TotaleTestata,
               ROUND(SUM(ImponibileRighe), 2) AS ImponibileMovIvaRg,
               ROUND(SUM(IvaRighe), 2) AS IvaMovIvaRg
        FROM verifica;

        SELECT COUNT(*) AS RigheOrfane
        FROM MovIvaRg r
        LEFT JOIN MovIva m ON m.ID = r.ID AND m.Settore = 10
        WHERE r.Settore = 10 AND m.ID IS NULL;

        SELECT COUNT(*) AS RigheConChiaviDiscordanti
        FROM MovIvaRg r
        INNER JOIN MovIva m ON m.ID = r.ID AND m.Settore = 10
        WHERE r.Settore = 10
          AND (COALESCE(r.Anno, 0) <> COALESCE(m.Anno, 0)
               OR COALESCE(r.Codice, 0) <> COALESCE(m.Codice, 0));

        SELECT COALESCE(AliqIva, 0) AS Aliquota, COUNT(*) AS Righe,
               ROUND(SUM(COALESCE(Imponibile, 0)), 2) AS Imponibile,
               ROUND(SUM(COALESCE(Iva, 0)), 2) AS Iva
        FROM MovIvaRg
        WHERE Settore = 10
        GROUP BY COALESCE(AliqIva, 0)
        ORDER BY Aliquota;

        SELECT
            SUM(CASE WHEN COALESCE(AliqIva, 0) = 0 AND ABS(COALESCE(Iva, 0)) > 0.005
                     THEN 1 ELSE 0 END) AS AliquotaZeroConIva,
            SUM(CASE WHEN COALESCE(AliqIva, 0) > 0
                          AND ABS(COALESCE(Iva, 0) - ROUND(COALESCE(Imponibile, 0) * AliqIva / 100, 2)) > 0.02
                     THEN 1 ELSE 0 END) AS IvaNonCoerenteConAliquota,
            SUM(CASE WHEN COALESCE(Imponibile, 0) < 0 OR COALESCE(Iva, 0) < 0
                     THEN 1 ELSE 0 END) AS RigheConImportiNegativi
        FROM MovIvaRg
        WHERE Settore = 10;
        """;

    await using var command = new MySqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();
    var resultNumber = 0;
    do
    {
        resultNumber++;
        while (await reader.ReadAsync())
        {
            if (resultNumber == 4)
            {
                Console.WriteLine(
                    $"Aliquota {reader["Aliquota"]}: {reader["Righe"]} righe; " +
                    $"imponibile {reader["Imponibile"]}; IVA {reader["Iva"]}");
                continue;
            }

            for (var index = 0; index < reader.FieldCount; index++)
            {
                Console.WriteLine($"{reader.GetName(index)}: {Convert.ToString(reader.GetValue(index))}");
            }
        }
    }
    while (await reader.NextResultAsync());
}

static async Task ShowIndexesAsync(MySqlConnection connection, string tableName)
{
    await using var command = new MySqlCommand(
        """
        SELECT INDEX_NAME,
               SEQ_IN_INDEX,
               COLUMN_NAME,
               NON_UNIQUE
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND LOWER(TABLE_NAME) = LOWER(@tableName)
        ORDER BY INDEX_NAME, SEQ_IN_INDEX;
        """,
        connection);
    command.Parameters.AddWithValue("@tableName", tableName);

    await using var reader = await command.ExecuteReaderAsync();
    var rows = new List<string[]>();
    while (await reader.ReadAsync())
    {
        rows.Add([
            reader.GetString("INDEX_NAME"),
            reader.GetInt32("SEQ_IN_INDEX").ToString(),
            reader.GetString("COLUMN_NAME"),
            reader.GetBoolean("NON_UNIQUE") ? "NO" : "SI"
        ]);
    }

    if (rows.Count == 0)
    {
        Console.WriteLine($"Nessun indice trovato per {tableName}.");
        return;
    }

    PrintTable(["Indice", "Seq", "Colonna", "Unico"], rows);
}

static async Task ShowCreateTableAsync(MySqlConnection connection, string tableName)
{
    await using var command = new MySqlCommand($"SHOW CREATE TABLE `{tableName.Replace("`", "``")}`;", connection);
    await using var reader = await command.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        Console.Error.WriteLine($"Tabella non trovata: {tableName}");
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine(reader.GetString(1));
}

static void PrintTable(string[] headers, List<string[]> rows)
{
    var widths = headers.Select(header => header.Length).ToArray();
    foreach (var row in rows)
    {
        for (var i = 0; i < row.Length; i++)
        {
            widths[i] = Math.Max(widths[i], row[i].Length);
        }
    }

    Console.WriteLine(string.Join("  ", headers.Select((header, i) => header.PadRight(widths[i]))));
    Console.WriteLine(string.Join("  ", widths.Select(width => new string('-', width))));
    foreach (var row in rows)
    {
        Console.WriteLine(string.Join("  ", row.Select((value, i) => value.PadRight(widths[i]))));
    }
}

static void PrintHelp()
{
    Console.WriteLine("Uso:");
    Console.WriteLine("  tools\\DbSchema.ps1 <Tabella>");
    Console.WriteLine("  tools\\DbSchema.ps1 <Tabella> --indexes");
    Console.WriteLine("  tools\\DbSchema.ps1 <Tabella> --create");
    Console.WriteLine("  tools\\DbSchema.ps1 --list");
    Console.WriteLine("  tools\\DbSchema.ps1 --sales-audit");
    Console.WriteLine("  tools\\DbSchema.ps1 --purchase-audit");
}

internal sealed class DbSchemaOptions
{
    public string? TableName { get; set; }

    public bool ListTables { get; set; }

    public bool ShowCreate { get; set; }

    public bool ShowIndexes { get; set; }

    public bool SalesAudit { get; set; }

    public bool PurchaseAudit { get; set; }

    public string? TargetDatabase { get; set; }

    public bool ShowHelp { get; set; }
}
