using MySqlConnector;

var localConnectionString = Environment.GetEnvironmentVariable("MICRONOTE_LOCAL_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione locale mancante.");
var remoteConnectionString = Environment.GetEnvironmentVariable("MICRONOTE_REMOTE_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione remota mancante.");
var apply = args.Any(arg => arg.Equals("--apply", StringComparison.OrdinalIgnoreCase));

localConnectionString = new MySqlConnectionStringBuilder(localConnectionString)
{
    Database = "Micronote_master"
}.ConnectionString;

await using var local = new MySqlConnection(localConnectionString);
await using var remote = new MySqlConnection(remoteConnectionString);
await local.OpenAsync();
await remote.OpenAsync();

foreach (var connection in new[] { local, remote })
{
    var duplicateCodes = await ScalarAsync(
        connection,
        "SELECT COUNT(*) FROM (SELECT Codice FROM Aziende GROUP BY Codice HAVING COUNT(*) > 1) d;");
    var duplicateNames = await ScalarAsync(
        connection,
        "SELECT COUNT(*) FROM (SELECT Nome FROM Aziende GROUP BY Nome HAVING COUNT(*) > 1) d;");
    var nullCodes = await ScalarAsync(connection, "SELECT COUNT(*) FROM Aziende WHERE Codice IS NULL;");
    if (duplicateCodes != 0 || duplicateNames != 0 || nullCodes != 0)
    {
        throw new InvalidOperationException(
            $"{connection.Database}.Aziende non può ricevere le chiavi: " +
            $"Codici duplicati={duplicateCodes}, nomi duplicati={duplicateNames}, codici nulli={nullCodes}.");
    }
}

Console.WriteLine("Controlli preliminari superati su locale e remoto.");
Console.WriteLine("Struttura risultante: campi completi, PK Codice, UX Nome, lunghezze maggiori, utf8mb4.");
if (!apply)
{
    Console.WriteLine("Rieseguire con --apply per applicare la migrazione.");
    return;
}

await AddColumnIfMissingAsync(remote, "ScadenzaLicenza", "date DEFAULT NULL AFTER `Bloccata`");
await AddColumnIfMissingAsync(remote, "UsaUsbKey", "tinyint DEFAULT NULL AFTER `ScadenzaLicenza`");
await AddColumnIfMissingAsync(remote, "UsbKeyCode", "varchar(30) DEFAULT NULL AFTER `UsaUsbKey`");
await AddColumnIfMissingAsync(remote, "DataAggiornamentoDb", "date DEFAULT NULL AFTER `VersioneDbRichiesta`");

foreach (var connection in new[] { local, remote })
{
    await ExecuteAsync(
        connection,
        """
        ALTER TABLE Aziende
          MODIFY COLUMN Nome varchar(120) NOT NULL,
          MODIFY COLUMN Password varchar(255) NOT NULL,
          MODIFY COLUMN Attiva tinyint(1) NOT NULL DEFAULT 1,
          MODIFY COLUMN Bloccata tinyint(1) NOT NULL DEFAULT 0,
          MODIFY COLUMN ScadenzaLicenza date DEFAULT NULL,
          MODIFY COLUMN UsaUsbKey tinyint DEFAULT NULL,
          MODIFY COLUMN UsbKeyCode varchar(30) DEFAULT NULL,
          MODIFY COLUMN NomeDatabase varchar(120) DEFAULT NULL,
          MODIFY COLUMN VersioneDbAttuale varchar(30) DEFAULT NULL,
          MODIFY COLUMN VersioneDbRichiesta varchar(30) DEFAULT NULL,
          MODIFY COLUMN DataAggiornamentoDb date DEFAULT NULL;
        """);
    await ExecuteAsync(
        connection,
        "ALTER TABLE Aziende CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
    await AddPrimaryKeyIfMissingAsync(connection);
    await AddUniqueNameIfMissingAsync(connection);
}

Console.WriteLine("Migrazione Aziende completata su Micronote_master e SmarterASP.");

static async Task AddColumnIfMissingAsync(
    MySqlConnection connection,
    string columnName,
    string definition)
{
    await using var check = new MySqlCommand(
        """
        SELECT COUNT(*) FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'aziende' AND COLUMN_NAME = @columnName;
        """,
        connection);
    check.Parameters.AddWithValue("@columnName", columnName);
    if (Convert.ToInt32(await check.ExecuteScalarAsync()) == 0)
    {
        await ExecuteAsync(connection, $"ALTER TABLE Aziende ADD COLUMN `{columnName}` {definition};");
    }
}

static async Task AddPrimaryKeyIfMissingAsync(MySqlConnection connection)
{
    var count = await ScalarAsync(
        connection,
        """
        SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
        WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'aziende'
          AND CONSTRAINT_TYPE = 'PRIMARY KEY';
        """);
    if (count == 0)
    {
        await ExecuteAsync(connection, "ALTER TABLE Aziende ADD PRIMARY KEY (Codice);");
    }
}

static async Task AddUniqueNameIfMissingAsync(MySqlConnection connection)
{
    var count = await ScalarAsync(
        connection,
        """
        SELECT COUNT(*) FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'aziende'
          AND COLUMN_NAME = 'Nome' AND NON_UNIQUE = 0;
        """);
    if (count == 0)
    {
        await ExecuteAsync(connection, "ALTER TABLE Aziende ADD UNIQUE KEY UX_Aziende_Nome (Nome);");
    }
}

static async Task<long> ScalarAsync(MySqlConnection connection, string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}

static async Task ExecuteAsync(MySqlConnection connection, string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}
