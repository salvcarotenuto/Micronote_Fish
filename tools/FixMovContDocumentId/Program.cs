using MySqlConnector;

var connectionString = Environment.GetEnvironmentVariable("MICRONOTE_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione MySQL mancante.");

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

static async Task<long> ScalarAsync(MySqlConnection connection, MySqlTransaction? transaction, string sql)
{
    await using var command = new MySqlCommand(sql, connection, transaction);
    var value = await command.ExecuteScalarAsync();
    return Convert.ToInt64(value);
}

static async Task<int> ExecuteAsync(MySqlConnection connection, MySqlTransaction transaction, string sql)
{
    await using var command = new MySqlCommand(sql, connection, transaction);
    return await command.ExecuteNonQueryAsync();
}

static async Task DumpAsync(MySqlConnection connection, string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();
    var names = Enumerable.Range(0, reader.FieldCount)
        .Select(reader.GetName)
        .ToArray();

    Console.WriteLine(string.Join("\t", names));
    while (await reader.ReadAsync())
    {
        Console.WriteLine(string.Join(
            "\t",
            names.Select(name => reader[name] is DBNull ? "NULL" : Convert.ToString(reader[name]))));
    }
}

var hasDocument = await ScalarAsync(
    connection,
    null,
    """
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'movcont'
      AND COLUMN_NAME = 'Documento';
    """);

if (hasDocument == 0)
{
    Console.WriteLine("ERRORE: la colonna Documento non esiste in movcont.");
    return 2;
}

var dataType = "";
await using (var typeCommand = new MySqlCommand(
    """
    SELECT DATA_TYPE
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'movcont'
      AND COLUMN_NAME = 'Documento';
    """,
    connection))
{
    dataType = Convert.ToString(await typeCommand.ExecuteScalarAsync()) ?? "";
}

Console.WriteLine($"Tipo iniziale movcont.Documento: {dataType}");
Console.WriteLine("Prima:");
Console.WriteLine($"  MovCont settore 20: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM movcont WHERE Settore = 20;")}");
Console.WriteLine($"  Documento vuoto/null: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM movcont WHERE Documento IS NULL OR TRIM(CAST(Documento AS CHAR)) = '';")}");
Console.WriteLine($"  Documento vecchio formato VN...: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM movcont WHERE Documento REGEXP '^VN[0-9]{7,}$';")}");
Console.WriteLine($"  Documento non numerico: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM movcont WHERE Documento IS NOT NULL AND TRIM(CAST(Documento AS CHAR)) <> '' AND Documento NOT REGEXP '^[0-9]+$';")}");

Console.WriteLine("Primi documenti vendita vecchio formato non risolvibili:");
await DumpAsync(
    connection,
    """
    SELECT m.ID, m.Anno, m.Codice, m.Documento
    FROM movcont m
    LEFT JOIN Vendite v
      ON v.Anno = CAST(SUBSTRING(m.Documento, 3, 4) AS UNSIGNED)
     AND v.Codice = CAST(SUBSTRING(m.Documento, 9) AS UNSIGNED)
    WHERE m.Settore = 20
      AND m.Documento REGEXP '^VN[0-9]{7,}$'
      AND v.ID IS NULL
    ORDER BY m.Anno DESC, m.Codice DESC
    LIMIT 40;
    """);

await using var transaction = await connection.BeginTransactionAsync();
try
{
    var updatedSalesDocuments = await ExecuteAsync(
        connection,
        transaction,
        """
        UPDATE movcont m
        INNER JOIN (
            SELECT ID,
                   CAST(SUBSTRING(Documento, 3, 4) AS UNSIGNED) AS DocAnno,
                   CAST(SUBSTRING(Documento, 9) AS UNSIGNED) AS DocCodice
            FROM movcont
            WHERE Settore = 20
              AND Documento REGEXP '^VN[0-9]{7,}$'
        ) parsed ON parsed.ID = m.ID
        INNER JOIN Vendite v
           ON v.Anno = parsed.DocAnno
          AND v.Codice = parsed.DocCodice
        SET m.Documento = v.ID
        WHERE m.Settore = 20;
        """);

    var clearedInvalidDocuments = await ExecuteAsync(
        connection,
        transaction,
        """
        UPDATE movcont
        SET Documento = NULL
        WHERE Documento IS NOT NULL
          AND TRIM(CAST(Documento AS CHAR)) <> ''
          AND Documento NOT REGEXP '^[0-9]+$';
        """);

    var clearedEmptyDocuments = await ExecuteAsync(
        connection,
        transaction,
        """
        UPDATE movcont
        SET Documento = NULL
        WHERE Documento IS NOT NULL
          AND TRIM(CAST(Documento AS CHAR)) = '';
        """);

    await transaction.CommitAsync();
    Console.WriteLine($"Riclassificati documenti vendita: {updatedSalesDocuments}");
    Console.WriteLine($"Azzerati documenti non numerici residui: {clearedInvalidDocuments}");
    Console.WriteLine($"Azzerati documenti vuoti: {clearedEmptyDocuments}");
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

var finalType = dataType;
if (!string.Equals(dataType, "int", StringComparison.OrdinalIgnoreCase))
{
    await using var alterCommand = new MySqlCommand(
        "ALTER TABLE movcont MODIFY Documento INT NULL;",
        connection);
    await alterCommand.ExecuteNonQueryAsync();
    finalType = "int";
}

Console.WriteLine("Dopo:");
Console.WriteLine($"  Tipo finale movcont.Documento: {finalType}");
Console.WriteLine($"  Documento non numerico: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM movcont WHERE Documento IS NOT NULL AND TRIM(CAST(Documento AS CHAR)) <> '' AND Documento NOT REGEXP '^[0-9]+$';")}");
Console.WriteLine($"  MovCont vendite collegati a Vendite.ID: {await ScalarAsync(connection, null, """
    SELECT COUNT(*)
    FROM movcont m
    INNER JOIN Vendite v ON v.ID = m.Documento
    WHERE m.Settore = 20;
    """)}");

return 0;
