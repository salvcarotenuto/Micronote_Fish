using System.Globalization;
using MySqlConnector;

const string targetDatabase = "MicroFish_0001";
var connectionString = Environment.GetEnvironmentVariable("MICROFISH_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione MySQL mancante.");
if (args.Length == 1
    && args[0].Equals("--activate-all", StringComparison.OrdinalIgnoreCase))
{
    var activationBuilder = new MySqlConnectionStringBuilder(connectionString)
    {
        Database = targetDatabase
    };
    await using var activationConnection =
        new MySqlConnection(activationBuilder.ConnectionString);
    await activationConnection.OpenAsync();
    await using var activationCommand = new MySqlCommand(
        "UPDATE clienti SET Attivo = 1 WHERE COALESCE(Attivo, 0) <> 1;",
        activationConnection);
    var updated = await activationCommand.ExecuteNonQueryAsync();
    await using var activeCountCommand = new MySqlCommand(
        "SELECT COUNT(*) FROM clienti WHERE Attivo = 1;",
        activationConnection);
    var active = Convert.ToInt32(await activeCountCommand.ExecuteScalarAsync());
    Console.WriteLine($"Clienti aggiornati: {updated}.");
    Console.WriteLine($"Clienti attivi: {active}.");
    return;
}
if (args.Length != 1)
{
    throw new InvalidOperationException("Uso: ImportLegacyClients <clienti.tsv>");
}

var lines = await File.ReadAllLinesAsync(Path.GetFullPath(args[0]));
if (lines.Length < 2)
{
    throw new InvalidOperationException("Il file non contiene clienti.");
}

var headers = lines[0].Split('\t');
var positions = headers
    .Select((name, index) => (name, index))
    .ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);
var builder = new MySqlConnectionStringBuilder(connectionString)
{
    Database = targetDatabase
};
await using var connection = new MySqlConnection(builder.ConnectionString);
await connection.OpenAsync();

await AddColumnIfMissingAsync(
    connection,
    "Listino",
    "TINYINT NOT NULL DEFAULT 0 AFTER Categoria");
await AddColumnIfMissingAsync(
    connection,
    "Fido",
    "DECIMAL(12,2) NOT NULL DEFAULT 0 AFTER Listino");

var rowsBefore = await ScalarAsync(connection, "SELECT COUNT(*) FROM clienti;");
await using var transaction = await connection.BeginTransactionAsync();
var imported = 0;
var shortened = 0;
try
{
    foreach (var line in lines.Skip(1).Where(value => !string.IsNullOrWhiteSpace(value)))
    {
        var fields = line.Split('\t');
        string Text(string name, int length)
        {
            var value = fields[positions[name]].Trim();
            if (value.Length <= length)
            {
                return value;
            }

            shortened++;
            return value[..length];
        }
        int Number(string name) =>
            int.TryParse(
                fields[positions[name]],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value)
                ? value
                : 0;
        decimal Amount(string name) =>
            decimal.TryParse(
                fields[positions[name]],
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var value)
                ? value
                : 0m;

        const string sql = """
            INSERT INTO clienti
            (
                Codice, Nome, Codfi, Piva, Citta, Cap, Prov, Via,
                Telefono, Cellulare, Email, Pec, Contatto, ContCell,
                PuntoV, Attivo, Categoria, Listino, Fido, SaldoIni,
                CodSdi, Nazione, Natura,
                Contropartita, Agente, Pagamento, Banca
            )
            VALUES
            (
                @code, @name, @taxCode, @vatNumber, @city, @postalCode,
                @province, @address, @phone, @mobile, @email, @pec, '', '',
                @storeCode, @active, @categoryCode, @priceList, @creditLimit,
                @openingBalance,
                @sdiCode, @countryCode, @legalNature, @accountCode,
                @agentCode, @paymentCode, @bankCode
            )
            ON DUPLICATE KEY UPDATE
                Nome = VALUES(Nome),
                Codfi = VALUES(Codfi),
                Piva = VALUES(Piva),
                Citta = VALUES(Citta),
                Cap = VALUES(Cap),
                Prov = VALUES(Prov),
                Via = VALUES(Via),
                Telefono = VALUES(Telefono),
                Cellulare = VALUES(Cellulare),
                Email = VALUES(Email),
                Pec = VALUES(Pec),
                PuntoV = VALUES(PuntoV),
                Attivo = VALUES(Attivo),
                Categoria = VALUES(Categoria),
                Listino = VALUES(Listino),
                Fido = VALUES(Fido),
                SaldoIni = VALUES(SaldoIni),
                CodSdi = VALUES(CodSdi),
                Nazione = VALUES(Nazione),
                Natura = VALUES(Natura),
                Contropartita = VALUES(Contropartita),
                Agente = VALUES(Agente),
                Pagamento = VALUES(Pagamento),
                Banca = VALUES(Banca);
            """;
        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@code", Number("Codice"));
        command.Parameters.AddWithValue("@name", Text("Nome", 250));
        command.Parameters.AddWithValue("@taxCode", Text("CodFi", 16));
        command.Parameters.AddWithValue("@vatNumber", Text("Piva", 11));
        command.Parameters.AddWithValue("@city", Text("Citta", 50));
        command.Parameters.AddWithValue("@postalCode", Text("Cap", 5));
        command.Parameters.AddWithValue("@province", Text("Provincia", 2));
        command.Parameters.AddWithValue("@address", Text("Via", 50));
        command.Parameters.AddWithValue("@phone", Text("Telefono1", 30));
        command.Parameters.AddWithValue("@mobile", Text("Telefono2", 30));
        command.Parameters.AddWithValue("@email", Text("Email", 50));
        command.Parameters.AddWithValue("@pec", Text("Pec", 50));
        command.Parameters.AddWithValue("@storeCode", Number("PuntoV"));
        command.Parameters.AddWithValue("@active", Number("Attivo"));
        command.Parameters.AddWithValue("@categoryCode", Number("Categoria"));
        command.Parameters.AddWithValue("@priceList", Number("Listino"));
        command.Parameters.AddWithValue("@creditLimit", Amount("Fido"));
        command.Parameters.AddWithValue("@openingBalance", Amount("SaldoIni"));
        command.Parameters.AddWithValue("@sdiCode", Text("CodSdi", 10));
        command.Parameters.AddWithValue("@countryCode", Number("Nazione"));
        command.Parameters.AddWithValue("@legalNature", Number("Natura"));
        command.Parameters.AddWithValue("@accountCode", Number("Contropartita"));
        command.Parameters.AddWithValue("@agentCode", Number("Agente"));
        command.Parameters.AddWithValue("@paymentCode", Number("Pagamento"));
        command.Parameters.AddWithValue("@bankCode", Number("Banca"));
        await command.ExecuteNonQueryAsync();
        imported++;
    }

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

await using var verify = new MySqlCommand(
    """
    SELECT
        COUNT(*),
        COALESCE(MIN(Codice), 0),
        COALESCE(MAX(Codice), 0),
        SUM(CASE WHEN Attivo = 1 THEN 1 ELSE 0 END)
    FROM clienti;
    """,
    connection);
await using var reader = await verify.ExecuteReaderAsync();
await reader.ReadAsync();
Console.WriteLine($"Righe destinazione prima del travaso: {rowsBefore}.");
Console.WriteLine($"Clienti letti dal legacy: {imported}.");
Console.WriteLine(
    $"Verifica tabella: righe={reader.GetInt32(0)}, min={reader.GetInt32(1)}, " +
    $"max={reader.GetInt32(2)}, attivi={reader.GetInt32(3)}.");
Console.WriteLine($"Valori accorciati per i limiti dello schema moderno: {shortened}.");
await reader.DisposeAsync();

var referenceAudits = new[]
{
    ("categorie mancanti", """
        SELECT COUNT(*) FROM clienti c
        WHERE COALESCE(c.Categoria, 0) > 0
          AND NOT EXISTS (
              SELECT 1 FROM CategoriaCF x WHERE x.Codice = c.Categoria);
        """),
    ("punti vendita mancanti", """
        SELECT COUNT(*) FROM clienti c
        WHERE COALESCE(c.PuntoV, 0) > 0
          AND NOT EXISTS (
              SELECT 1 FROM puntivendita x WHERE x.Codice = c.PuntoV);
        """),
    ("contropartite mancanti", """
        SELECT COUNT(*) FROM clienti c
        WHERE COALESCE(c.Contropartita, 0) > 0
          AND NOT EXISTS (
              SELECT 1 FROM conti x WHERE x.Codice = c.Contropartita);
        """),
    ("pagamenti mancanti", """
        SELECT COUNT(*) FROM clienti c
        WHERE COALESCE(c.Pagamento, 0) > 0
          AND NOT EXISTS (
              SELECT 1 FROM pagamenti x WHERE x.Codice = c.Pagamento);
        """),
    ("banche mancanti", """
        SELECT COUNT(*) FROM clienti c
        WHERE COALESCE(c.Banca, 0) > 0
          AND NOT EXISTS (
              SELECT 1 FROM banche x WHERE x.Codice = c.Banca);
        """)
};
foreach (var (label, sql) in referenceAudits)
{
    Console.WriteLine($"Controllo {label}: {await ScalarAsync(connection, sql)}.");
}
Console.WriteLine(
    $"Clienti con Listino valorizzato: " +
    $"{await ScalarAsync(connection, "SELECT COUNT(*) FROM clienti WHERE Listino <> 0;")}.");
Console.WriteLine(
    $"Clienti con Fido valorizzato: " +
    $"{await ScalarAsync(connection, "SELECT COUNT(*) FROM clienti WHERE Fido <> 0;")}.");

static async Task<int> ScalarAsync(MySqlConnection connection, string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    return Convert.ToInt32(await command.ExecuteScalarAsync());
}

static async Task AddColumnIfMissingAsync(
    MySqlConnection connection,
    string column,
    string definition)
{
    await using var check = new MySqlCommand(
        """
        SELECT COUNT(*)
        FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'clienti'
          AND COLUMN_NAME = @column;
        """,
        connection);
    check.Parameters.AddWithValue("@column", column);
    if (Convert.ToInt32(await check.ExecuteScalarAsync()) == 0)
    {
        await using var alter = new MySqlCommand(
            $"ALTER TABLE clienti ADD COLUMN `{column}` {definition};",
            connection);
        await alter.ExecuteNonQueryAsync();
    }
}
