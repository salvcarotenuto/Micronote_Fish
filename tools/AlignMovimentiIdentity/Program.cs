using MySqlConnector;

var connectionString = Environment.GetEnvironmentVariable("MICRONOTE_DB_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("MICRONOTE_DB_CONNECTION non valorizzata.");

var apply = args.Any(arg => arg.Equals("--apply", StringComparison.OrdinalIgnoreCase));
var databases = args.Where(arg => !arg.StartsWith("--", StringComparison.Ordinal)).ToArray();
if (databases.Length == 0)
    throw new InvalidOperationException("Indicare almeno un database.");

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

foreach (var database in databases)
{
    await connection.ChangeDatabaseAsync(database);
    var duplicateGroups = await ScalarIntAsync(connection, """
        SELECT COUNT(*)
        FROM (
            SELECT Anno, Codice, Riga
            FROM Movimenti
            GROUP BY Anno, Codice, Riga
            HAVING COUNT(*) > 1
        ) duplicates;
        """);
    var hasIdentity = await ScalarIntAsync(connection, """
        SELECT COUNT(*)
        FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='movimenti'
          AND COLUMN_NAME='ID' AND EXTRA LIKE '%auto_increment%';
        """) == 1;
    var hasUniqueKey = await ScalarIntAsync(connection, """
        SELECT COUNT(*)
        FROM (
            SELECT INDEX_NAME
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='movimenti' AND NON_UNIQUE=0
            GROUP BY INDEX_NAME
            HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='Anno,Codice,Riga'
        ) indexes_found;
        """) > 0;

    Console.WriteLine($"{database}: ID identity={hasIdentity}, duplicati={duplicateGroups}, UX Anno/Codice/Riga={hasUniqueKey}");
    if (!hasIdentity)
        throw new InvalidOperationException($"{database}: ID AUTO_INCREMENT non presente.");
    if (duplicateGroups > 0)
        throw new InvalidOperationException($"{database}: impossibile creare l'indice, presenti {duplicateGroups} gruppi duplicati.");
    if (!apply)
        continue;

    if (!hasUniqueKey)
        await ExecuteAsync(connection, "ALTER TABLE Movimenti ADD UNIQUE KEY UX_Movimenti_Anno_Codice_Riga (Anno, Codice, Riga);");
    Console.WriteLine($"{database}: allineamento applicato.");
}

static async Task<int> ScalarIntAsync(MySqlConnection connection, string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    return Convert.ToInt32(await command.ExecuteScalarAsync());
}

static async Task ExecuteAsync(MySqlConnection connection, string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}
