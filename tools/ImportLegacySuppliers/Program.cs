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
        "UPDATE fornitori SET Attivo = 1 WHERE COALESCE(Attivo, 0) <> 1;",
        activationConnection);
    var updated = await activationCommand.ExecuteNonQueryAsync();
    await using var activeCountCommand = new MySqlCommand(
        "SELECT COUNT(*) FROM fornitori WHERE Attivo = 1;",
        activationConnection);
    var active = Convert.ToInt32(await activeCountCommand.ExecuteScalarAsync());
    Console.WriteLine($"Fornitori aggiornati: {updated}.");
    Console.WriteLine($"Fornitori attivi: {active}.");
    return;
}
if (args.Length == 1
    && args[0].Equals("--inspect-stores", StringComparison.OrdinalIgnoreCase))
{
    var inspectBuilder = new MySqlConnectionStringBuilder(connectionString)
    {
        Database = targetDatabase
    };
    await using var inspectConnection = new MySqlConnection(inspectBuilder.ConnectionString);
    await inspectConnection.OpenAsync();
    foreach (var sql in new[]
    {
        "SELECT Codice, Nome FROM puntivendita ORDER BY Codice;",
        "SELECT PuntoV, COUNT(*) AS Fornitori FROM fornitori GROUP BY PuntoV ORDER BY PuntoV;"
    })
    {
        await using var inspectCommand = new MySqlCommand(sql, inspectConnection);
        await using var inspectReader = await inspectCommand.ExecuteReaderAsync();
        Console.WriteLine(sql);
        while (await inspectReader.ReadAsync())
        {
            Console.WriteLine(
                $"{Convert.ToString(inspectReader.GetValue(0), CultureInfo.InvariantCulture)}\t" +
                $"{Convert.ToString(inspectReader.GetValue(1), CultureInfo.InvariantCulture)}");
        }
    }
    return;
}
if (args.Length != 1)
{
    throw new InvalidOperationException("Uso: ImportLegacySuppliers <fornitori.tsv>");
}

var lines = await File.ReadAllLinesAsync(Path.GetFullPath(args[0]));
if (lines.Length < 2)
{
    throw new InvalidOperationException("Il file non contiene fornitori.");
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
    "Categoria",
    "SMALLINT NULL DEFAULT 0 AFTER Attivo");
await AddColumnIfMissingAsync(
    connection,
    "Fido",
    "DECIMAL(12,2) NOT NULL DEFAULT 0 AFTER Categoria");

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
            if (value.Length <= length) return value;
            shortened++;
            return value[..length];
        }
        int Number(string name) =>
            int.TryParse(fields[positions[name]], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0;
        decimal Amount(string name) =>
            decimal.TryParse(fields[positions[name]], NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0m;

        const string sql = """
            INSERT INTO fornitori
            (
                Codice, Nome, Codfi, Piva, Citta, Cap, Prov, Via,
                Telefono, Cellulare, Email, Pec, Contatto, ContCell,
                PuntoV, Attivo, Categoria, Fido, SaldoIni, CodSdi, Nazione, Natura,
                Contropartita, Agente, Pagamento, Banca
            )
            VALUES
            (
                @code, @name, @taxCode, @vatNumber, @city, @postalCode,
                @province, @address, @phone, @mobile, @email, @pec, NULL, NULL,
                @storeCode, @active, @categoryCode, @creditLimit, @openingBalance,
                @sdiCode, @countryCode,
                @legalNature, @accountCode, @agentCode, @paymentCode, @bankCode
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
        command.Parameters.AddWithValue("@taxCode", Text("Codfi", 16));
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
    "SELECT COUNT(*), MIN(Codice), MAX(Codice) FROM fornitori;",
    connection);
await using var reader = await verify.ExecuteReaderAsync();
await reader.ReadAsync();
Console.WriteLine($"Fornitori importati: {imported}.");
Console.WriteLine($"Verifica tabella: righe={reader.GetInt32(0)}, min={reader.GetInt32(1)}, max={reader.GetInt32(2)}.");
Console.WriteLine($"Valori accorciati per limiti dello schema moderno: {shortened}.");

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
          AND TABLE_NAME = 'fornitori'
          AND COLUMN_NAME = @column;
        """,
        connection);
    check.Parameters.AddWithValue("@column", column);
    if (Convert.ToInt32(await check.ExecuteScalarAsync()) == 0)
    {
        await using var alter = new MySqlCommand(
            $"ALTER TABLE fornitori ADD COLUMN `{column}` {definition};",
            connection);
        await alter.ExecuteNonQueryAsync();
    }
}
