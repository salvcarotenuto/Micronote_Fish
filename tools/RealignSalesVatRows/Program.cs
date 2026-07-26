using MySqlConnector;

var connectionString = Environment.GetEnvironmentVariable("MICRONOTE_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione mancante.");
var apply = args.Any(arg => arg.Equals("--apply", StringComparison.OrdinalIgnoreCase));
var renameTotal = args.Any(arg => arg.Equals("--rename-total", StringComparison.OrdinalIgnoreCase));
var databaseArgument = args
    .FirstOrDefault(arg => arg.StartsWith("--database=", StringComparison.OrdinalIgnoreCase));
var targetDatabase = databaseArgument is null
    ? null
    : databaseArgument["--database=".Length..].Trim();
if (!string.IsNullOrWhiteSpace(targetDatabase))
{
    connectionString = new MySqlConnectionStringBuilder(connectionString)
    {
        Database = targetDatabase
    }.ConnectionString;
}
var backupName = $"MovIvaRg_PreSalesRealign_{DateTime.Now:yyyyMMdd_HHmmss}";

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();
Console.WriteLine($"Database: {connection.Database}");

var hasRetiredTotal = await ScalarAsync(connection, null, """
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Vendite' AND COLUMN_NAME = '_Totale';
    """);
var hasTotal = await ScalarAsync(connection, null, """
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Vendite' AND COLUMN_NAME = 'Totale';
    """);
var salesTotalColumn = (hasRetiredTotal, hasTotal) switch
{
    (1, 0) => "`_Totale`",
    (0, 1) => "`Totale`",
    _ => throw new InvalidOperationException(
        $"Schema Vendite non riconosciuto: Totale={hasTotal}, _Totale={hasRetiredTotal}.")
};
Console.WriteLine($"Colonna totale vendite: Vendite.{salesTotalColumn.Trim('`')}");

var sales = await ScalarAsync(connection, null, "SELECT COUNT(*) FROM Vendite;");
var invalidHeaders = await ScalarAsync(connection, null, """
    SELECT COUNT(*)
    FROM (
        SELECT v.Anno, v.Codice, COUNT(mi.ID) AS Testate
        FROM Vendite v
        LEFT JOIN MovIva mi ON mi.Anno = v.Anno AND mi.Codice = v.Codice AND mi.Settore = 20
        GROUP BY v.Anno, v.Codice
        HAVING Testate <> 1
    ) anomalie;
    """);
var inconsistentDetails = await ScalarAsync(connection, null, $"""
    SELECT COUNT(*)
    FROM Vendite v
    LEFT JOIN (
        SELECT Anno, Codice, SUM(COALESCE(Totale, 0)) AS Totale, SUM(COALESCE(Altro, 0)) AS Altro
        FROM VenditeRg
        GROUP BY Anno, Codice
    ) vr ON vr.Anno = v.Anno AND vr.Codice = v.Codice
    WHERE ABS(COALESCE(v.{salesTotalColumn}, 0) - COALESCE(vr.Totale, 0)) > 0.02
       OR ABS(COALESCE(v.Altro, 0) - COALESCE(vr.Altro, 0)) > 0.02;
    """);
var invalidAmounts = await ScalarAsync(connection, null, """
    SELECT COUNT(*)
    FROM VenditeRg
    WHERE COALESCE(Totale, 0) < 0
       OR COALESCE(Altro, 0) < 0
       OR COALESCE(Altro, 0) - COALESCE(Totale, 0) > 0.02;
    """);

Console.WriteLine($"Vendite: {sales}");
Console.WriteLine($"Testate MovIva mancanti/duplicate: {invalidHeaders}");
Console.WriteLine($"Dettagli non coerenti con la testata: {inconsistentDetails}");
Console.WriteLine($"Importi dettaglio non validi: {invalidAmounts}");

if (invalidHeaders != 0 || inconsistentDetails != 0 || invalidAmounts != 0)
{
    throw new InvalidOperationException("Controlli preliminari non superati: nessun dato modificato.");
}

if (!apply)
{
    Console.WriteLine("Controlli superati. Rieseguire con --apply per effettuare il riallineamento.");
    return;
}

await ExecuteAsync(connection, null, $"CREATE TABLE `{backupName}` LIKE MovIvaRg;");
await ExecuteAsync(connection, null, $"INSERT INTO `{backupName}` SELECT * FROM MovIvaRg;");
var originalRows = await ScalarAsync(connection, null, "SELECT COUNT(*) FROM MovIvaRg;");
var backupRows = await ScalarAsync(connection, null, $"SELECT COUNT(*) FROM `{backupName}`;");
if (originalRows != backupRows)
{
    throw new InvalidOperationException("Backup non coerente: riallineamento interrotto.");
}

await using var transaction = await connection.BeginTransactionAsync();
try
{
    await ExecuteAsync(connection, transaction, """
        DELETE mir
        FROM MovIvaRg mir
        INNER JOIN Vendite v ON v.Anno = mir.Anno AND v.Codice = mir.Codice
        WHERE mir.Settore = 20;
        """);

    await ExecuteAsync(connection, transaction, """
        INSERT INTO MovIvaRg (ID, Anno, Settore, Codice, PuntoV, AliqIva, Imponibile, Iva)
        SELECT mi.ID, vr.Anno, 20, vr.Codice, vr.PuntoV, 0, ROUND(vr.Altro, 2), 0
        FROM VenditeRg vr
        INNER JOIN MovIva mi ON mi.Anno = vr.Anno AND mi.Codice = vr.Codice AND mi.Settore = 20
        WHERE COALESCE(vr.Altro, 0) <> 0;
        """);

    await ExecuteAsync(connection, transaction, """
        INSERT INTO MovIvaRg (ID, Anno, Settore, Codice, PuntoV, AliqIva, Imponibile, Iva)
        SELECT mi.ID, vr.Anno, 20, vr.Codice, vr.PuntoV, 10,
               ROUND((vr.Totale - vr.Altro) / 1.10, 2),
               ROUND((vr.Totale - vr.Altro) - ROUND((vr.Totale - vr.Altro) / 1.10, 2), 2)
        FROM VenditeRg vr
        INNER JOIN MovIva mi ON mi.Anno = vr.Anno AND mi.Codice = vr.Codice AND mi.Settore = 20
        WHERE COALESCE(vr.Totale, 0) - COALESCE(vr.Altro, 0) > 0.005;
        """);

    var notBalanced = await ScalarAsync(connection, transaction, $"""
        SELECT COUNT(*)
        FROM Vendite v
        LEFT JOIN (
            SELECT Anno, Codice,
                   SUM(COALESCE(Imponibile, 0) + COALESCE(Iva, 0)) AS TotaleLordo
            FROM MovIvaRg
            WHERE Settore = 20
            GROUP BY Anno, Codice
        ) mir ON mir.Anno = v.Anno AND mir.Codice = v.Codice
        WHERE ABS(COALESCE(v.{salesTotalColumn}, 0) - COALESCE(mir.TotaleLordo, 0)) > 0.02;
        """);

    if (notBalanced != 0)
    {
        throw new InvalidOperationException($"{notBalanced} vendite non quadrano dopo la ricostruzione.");
    }

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

var insertedRows = await ScalarAsync(connection, null, """
    SELECT COUNT(*)
    FROM MovIvaRg mir
    INNER JOIN Vendite v ON v.Anno = mir.Anno AND v.Codice = mir.Codice
    WHERE mir.Settore = 20;
    """);
var zeroRateRows = await ScalarAsync(connection, null, """
    SELECT COUNT(*) FROM MovIvaRg mir
    INNER JOIN Vendite v ON v.Anno = mir.Anno AND v.Codice = mir.Codice
    WHERE mir.Settore = 20 AND COALESCE(mir.AliqIva, 0) = 0;
    """);
var tenRateRows = insertedRows - zeroRateRows;

Console.WriteLine($"Backup creato: {backupName} ({backupRows} righe).");
Console.WriteLine($"Righe vendite rigenerate: {insertedRows}");
Console.WriteLine($"Righe aliquota 0: {zeroRateRows}");
Console.WriteLine($"Righe aliquota 10: {tenRateRows}");
Console.WriteLine("Quadratura finale: tutte le vendite risultano coerenti.");

if (renameTotal)
{
    if (hasRetiredTotal == 1 && hasTotal == 0)
    {
        Console.WriteLine("Colonna già rinominata: Vendite._Totale.");
        return;
    }

    if (hasTotal != 1 || hasRetiredTotal != 0)
    {
        throw new InvalidOperationException(
            $"Rinomina non eseguita: Totale={hasTotal}, _Totale={hasRetiredTotal}.");
    }

    await ExecuteAsync(connection, null, "ALTER TABLE Vendite RENAME COLUMN Totale TO _Totale;");
    Console.WriteLine("Colonna rinominata: Vendite.Totale -> Vendite._Totale.");
}

static async Task<long> ScalarAsync(
    MySqlConnection connection,
    MySqlTransaction? transaction,
    string sql)
{
    await using var command = new MySqlCommand(sql, connection, transaction);
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}

static async Task ExecuteAsync(
    MySqlConnection connection,
    MySqlTransaction? transaction,
    string sql)
{
    await using var command = new MySqlCommand(sql, connection, transaction);
    await command.ExecuteNonQueryAsync();
}
