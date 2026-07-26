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

var hasId = await ScalarAsync(
    connection,
    null,
    """
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'movcontrg'
      AND COLUMN_NAME = 'ID';
    """);

if (hasId == 0)
{
    Console.WriteLine("ERRORE: la colonna ID non esiste in movcontrg.");
    return 2;
}

var duplicatedMasters = await ScalarAsync(
    connection,
    null,
    """
    SELECT COUNT(*)
    FROM (
        SELECT Anno, Settore, Codice
        FROM movcont
        GROUP BY Anno, Settore, Codice
        HAVING COUNT(*) > 1
    ) d;
    """);

Console.WriteLine("Prima:");
Console.WriteLine($"  MovCont: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM movcont;")}");
Console.WriteLine($"  MovContRg: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM movcontrg;")}");
Console.WriteLine($"  MovContRg con ID nullo/zero: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM movcontrg WHERE ID IS NULL OR ID = 0;")}");
Console.WriteLine($"  MovContRg senza madre su Anno+Settore+Codice: {await ScalarAsync(connection, null, """
    SELECT COUNT(*)
    FROM movcontrg r
    LEFT JOIN movcont m
      ON m.Anno = r.Anno
     AND m.Settore = r.Settore
     AND m.Codice = r.Codice
    WHERE m.ID IS NULL;
    """)}");
Console.WriteLine($"  Chiavi madri duplicate: {duplicatedMasters}");
Console.WriteLine($"  MovContRg aggiornabili senza ambiguita: {await ScalarAsync(connection, null, """
    SELECT COUNT(*)
    FROM movcontrg r
    INNER JOIN (
        SELECT Anno, Settore, Codice, MIN(ID) AS ID
        FROM movcont
        GROUP BY Anno, Settore, Codice
        HAVING COUNT(*) = 1
    ) m
      ON m.Anno = r.Anno
     AND m.Settore = r.Settore
     AND m.Codice = r.Codice
    WHERE r.ID IS NULL
       OR r.ID = 0
       OR r.ID <> m.ID;
    """)}");
Console.WriteLine("Distribuzione MovContRg per anno/settore:");
await DumpAsync(
    connection,
    """
    SELECT Anno,
           Settore,
           COUNT(*) AS Righe,
           SUM(CASE WHEN ID IS NULL OR ID = 0 THEN 1 ELSE 0 END) AS IdVuoto,
           SUM(CASE WHEN ID IS NOT NULL AND ID <> 0 THEN 1 ELSE 0 END) AS IdPresente
    FROM movcontrg
    GROUP BY Anno, Settore
    ORDER BY Anno DESC, Settore;
    """);

Console.WriteLine("Prime righe MovContRg con ID nullo/zero:");
await DumpAsync(
    connection,
    """
    SELECT ID, Anno, Settore, Codice, Riga, Segno, Conto, Importo
    FROM movcontrg
    WHERE ID IS NULL OR ID = 0
    ORDER BY Anno DESC, Settore, Codice DESC, Segno DESC, Riga
    LIMIT 40;
    """);

if (duplicatedMasters > 0)
{
    Console.WriteLine("ERRORE: impossibile ricostruire ID perché esistono chiavi MovCont duplicate su Anno+Settore+Codice.");
    Console.WriteLine("Prime chiavi duplicate:");
    await DumpAsync(
        connection,
        """
        SELECT Anno, Settore, Codice, COUNT(*) AS Duplicati,
               GROUP_CONCAT(ID ORDER BY ID SEPARATOR ',') AS IDMovCont,
               GROUP_CONCAT(DATE_FORMAT(DataMov, '%d/%m/%Y') ORDER BY ID SEPARATOR ',') AS DateMov,
               GROUP_CONCAT(COALESCE(Causale, 0) ORDER BY ID SEPARATOR ',') AS Causali
        FROM movcont
        GROUP BY Anno, Settore, Codice
        HAVING COUNT(*) > 1
        ORDER BY Anno, Settore, Codice
        LIMIT 40;
        """);
    return 3;
}

await using var transaction = await connection.BeginTransactionAsync();
try
{
    var updated = await ExecuteAsync(
        connection,
        transaction,
        """
        UPDATE movcontrg r
        INNER JOIN movcont m
           ON m.Anno = r.Anno
          AND m.Settore = r.Settore
          AND m.Codice = r.Codice
        SET r.ID = m.ID
        WHERE r.ID IS NULL
           OR r.ID = 0
           OR r.ID <> m.ID;
        """);

    await transaction.CommitAsync();
    Console.WriteLine($"Aggiornate: {updated}");
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

Console.WriteLine("Dopo:");
Console.WriteLine($"  MovContRg con ID nullo/zero: {await ScalarAsync(connection, null, "SELECT COUNT(*) FROM movcontrg WHERE ID IS NULL OR ID = 0;")}");
Console.WriteLine($"  Join MovCont/MovContRg via ID: {await ScalarAsync(connection, null, """
    SELECT COUNT(*)
    FROM movcontrg r
    INNER JOIN movcont m ON m.ID = r.ID;
    """)}");

return 0;
