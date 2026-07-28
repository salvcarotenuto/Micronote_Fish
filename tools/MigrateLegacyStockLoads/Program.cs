using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine(
        "Uso: MigrateLegacyStockLoads <cartella-tsv> <database-mysql> [--verify-only]");
    return 2;
}

var sourceDirectory = Path.GetFullPath(args[0]);
var databaseName = args[1].Trim();
var verifyOnly = args.Length == 3
    && string.Equals(args[2], "--verify-only", StringComparison.OrdinalIgnoreCase);
if (!System.Text.RegularExpressions.Regex.IsMatch(databaseName, "^[A-Za-z0-9_]+$"))
{
    Console.Error.WriteLine("Nome database MySQL non valido.");
    return 3;
}

var headers = File.ReadLines(Path.Combine(sourceDirectory, "carico.tsv"))
    .Select(ParseHeader)
    .ToArray();
var rows = File.ReadLines(Path.Combine(sourceDirectory, "caricorg.tsv"))
    .Select(ParseRow)
    .ToArray();

EnsureUnique(headers.Select(row => (row.Year, row.Code)), "Carico(Anno, Codice)");
EnsureUnique(rows.Select(row => (row.Year, row.Code, row.RowNumber)), "CaricoRg(Anno, Codice, Riga)");

var headerKeys = headers.Select(row => (row.Year, row.Code)).ToHashSet();
var orphanSourceRows = rows
    .Where(row => !headerKeys.Contains((row.Year, row.Code)))
    .Take(10)
    .ToArray();
if (orphanSourceRows.Length != 0)
{
    throw new InvalidOperationException(
        $"Il CE contiene righe senza testata. Prima anomalia: " +
        $"{orphanSourceRows[0].Year}/{orphanSourceRows[0].Code}/{orphanSourceRows[0].RowNumber}.");
}

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<SecretsMarker>(optional: false)
    .Build();
var baseConnectionString =
    configuration.GetConnectionString("MicronoteServer")
    ?? configuration.GetConnectionString("MicronoteDb")
    ?? throw new InvalidOperationException("Connessione Micronote non configurata.");
var connectionString = new MySqlConnectionStringBuilder(baseConnectionString)
{
    Database = databaseName,
    AllowUserVariables = true,
    DefaultCommandTimeout = 300
}.ConnectionString;

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();
Console.WriteLine($"Origine CE: {headers.Length} testate, {rows.Length} righe.");
Console.WriteLine($"Destinazione: {connection.Database}.");

await EnsureNoTargetDuplicatesAsync(connection, "carico", "Anno, Codice");
await EnsureNoTargetDuplicatesAsync(connection, "caricorg", "Anno, Codice, Riga");

if (verifyOnly)
{
    await VerifyAsync(connection, headers, rows);
    Console.WriteLine("VERIFICA COMPLETATA.");
    return 0;
}

var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
var headerBackup = $"carico_bak_ce_{stamp}";
var rowBackup = $"caricorg_bak_ce_{stamp}";
await BackupTableAsync(connection, "carico", headerBackup);
await BackupTableAsync(connection, "caricorg", rowBackup);
Console.WriteLine($"Backup creati: {headerBackup}, {rowBackup}.");

await EnsureSchemaAsync(connection);
await EnsureIndexesAsync(connection);

await using var transaction = await connection.BeginTransactionAsync();
try
{
    var parentIds = new Dictionary<(int Year, int Code), int>();
    foreach (var header in headers)
    {
        var id = await FindHeaderIdAsync(connection, transaction, header.Year, header.Code);
        if (id == 0)
        {
            id = await InsertHeaderAsync(connection, transaction, header);
        }
        else
        {
            await UpdateHeaderAsync(connection, transaction, id, header);
        }

        parentIds[(header.Year, header.Code)] = id;
    }

    await DeleteMigratedRowsAsync(connection, transaction, headers);
    foreach (var row in rows)
    {
        await InsertRowAsync(connection, transaction, parentIds[(row.Year, row.Code)], row);
    }

    await RebuildParentIdsAsync(connection, transaction);
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

await VerifyAsync(connection, headers, rows);

Console.WriteLine("MIGRAZIONE COMPLETATA.");
Console.WriteLine($"Backup ripristinabili: {headerBackup}, {rowBackup}.");
return 0;

static HeaderRow ParseHeader(string line)
{
    var fields = line.Split('\t');
    if (fields.Length != 8) throw new InvalidDataException("Riga Carico TSV non valida.");
    return new(
        Integer(fields[0]),
        Integer(fields[1]),
        Text(fields[2]),
        DateOnly.ParseExact(fields[3], "yyyy-MM-dd", CultureInfo.InvariantCulture),
        Integer(fields[4]),
        Decimal(fields[5], 2),
        Decimal(fields[6], 2),
        Decimal(fields[7], 2));
}

static DetailRow ParseRow(string line)
{
    var fields = line.Split('\t');
    if (fields.Length != 15) throw new InvalidDataException("Riga CaricoRg TSV non valida.");
    return new(
        Integer(fields[0]),
        Integer(fields[1]),
        Integer(fields[2]),
        Integer(fields[3]),
        DateOnly.ParseExact(fields[4], "yyyy-MM-dd", CultureInfo.InvariantCulture),
        Text(fields[5]),
        Text(fields[6]),
        Decimal(fields[7], 3),
        Decimal(fields[8], 3),
        Decimal(fields[9], 2),
        Decimal(fields[10], 2),
        Decimal(fields[11], 2),
        Decimal(fields[12], 3),
        Decimal(fields[13], 3),
        Decimal(fields[14], 3));
}

static int Integer(string value) =>
    int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

static decimal Decimal(string value, int digits) =>
    Math.Round(
        decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
        digits,
        MidpointRounding.AwayFromZero);

static string Text(string value) =>
    Encoding.UTF8.GetString(Convert.FromBase64String(value));

static void EnsureUnique<T>(IEnumerable<T> keys, string label) where T : notnull
{
    var duplicate = keys.GroupBy(key => key).FirstOrDefault(group => group.Count() > 1);
    if (duplicate is not null)
    {
        throw new InvalidOperationException($"{label}: chiave duplicata {duplicate.Key}.");
    }
}

static async Task EnsureNoTargetDuplicatesAsync(
    MySqlConnection connection,
    string table,
    string columns)
{
    await using var command = new MySqlCommand(
        $"SELECT {columns}, COUNT(*) FROM `{table}` GROUP BY {columns} HAVING COUNT(*) > 1 LIMIT 1;",
        connection);
    await using var reader = await command.ExecuteReaderAsync();
    if (await reader.ReadAsync())
    {
        throw new InvalidOperationException(
            $"La destinazione contiene chiavi duplicate in {table}({columns}).");
    }
}

static async Task BackupTableAsync(
    MySqlConnection connection,
    string source,
    string backup)
{
    await using (var create = new MySqlCommand(
        $"CREATE TABLE `{backup}` LIKE `{source}`;",
        connection))
    {
        await create.ExecuteNonQueryAsync();
    }

    await using var copy = new MySqlCommand(
        $"INSERT INTO `{backup}` SELECT * FROM `{source}`;",
        connection);
    await copy.ExecuteNonQueryAsync();
}

static async Task EnsureSchemaAsync(MySqlConnection connection)
{
    await ExecuteAsync(
        connection,
        """
        ALTER TABLE caricorg
            MODIFY COLUMN Quantita DECIMAL(10,3) DEFAULT 0.000,
            MODIFY COLUMN Prezzo DECIMAL(10,3) DEFAULT 0.000;
        """);
    await AddColumnIfMissingAsync(connection, "caricorg", "Tara", "DECIMAL(10,3) DEFAULT 0.000");
    await AddColumnIfMissingAsync(connection, "caricorg", "PrNetto", "DECIMAL(10,3) DEFAULT 0.000");
    await AddColumnIfMissingAsync(connection, "caricorg", "PrIvato", "DECIMAL(10,3) DEFAULT 0.000");
}

static async Task AddColumnIfMissingAsync(
    MySqlConnection connection,
    string table,
    string column,
    string definition)
{
    await using var exists = new MySqlCommand(
        """
        SELECT COUNT(*)
        FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @table
          AND COLUMN_NAME = @column;
        """,
        connection);
    exists.Parameters.AddWithValue("@table", table);
    exists.Parameters.AddWithValue("@column", column);
    if (Convert.ToInt32(await exists.ExecuteScalarAsync()) != 0) return;
    await ExecuteAsync(connection, $"ALTER TABLE `{table}` ADD COLUMN `{column}` {definition};");
}

static async Task<int> FindHeaderIdAsync(
    MySqlConnection connection,
    MySqlTransaction transaction,
    int year,
    int code)
{
    await using var command = new MySqlCommand(
        "SELECT ID FROM carico WHERE Anno=@year AND Codice=@code LIMIT 1;",
        connection,
        transaction);
    command.Parameters.AddWithValue("@year", year);
    command.Parameters.AddWithValue("@code", code);
    return Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
}

static async Task<int> InsertHeaderAsync(
    MySqlConnection connection,
    MySqlTransaction transaction,
    HeaderRow row)
{
    await using var command = new MySqlCommand(
        """
        INSERT INTO carico
            (Anno, Codice, NumDoc, DataDoc, Fornitore, Merce, Iva, Totale,
             Causale, PuntoV, FeName)
        VALUES
            (@year, @code, @number, @date, @supplier, @goods, @vat, @total,
             10, 0, '');
        """,
        connection,
        transaction);
    AddHeaderParameters(command, row);
    await command.ExecuteNonQueryAsync();
    return checked((int)command.LastInsertedId);
}

static async Task UpdateHeaderAsync(
    MySqlConnection connection,
    MySqlTransaction transaction,
    int id,
    HeaderRow row)
{
    await using var command = new MySqlCommand(
        """
        UPDATE carico
        SET NumDoc=@number, DataDoc=@date, Fornitore=@supplier,
            Merce=@goods, Iva=@vat, Totale=@total
        WHERE ID=@id;
        """,
        connection,
        transaction);
    AddHeaderParameters(command, row);
    command.Parameters.AddWithValue("@id", id);
    await command.ExecuteNonQueryAsync();
}

static void AddHeaderParameters(MySqlCommand command, HeaderRow row)
{
    command.Parameters.AddWithValue("@year", row.Year);
    command.Parameters.AddWithValue("@code", row.Code);
    command.Parameters.AddWithValue("@number", row.DocumentNumber);
    command.Parameters.Add("@date", MySqlDbType.Date).Value =
        row.DocumentDate.ToDateTime(TimeOnly.MinValue);
    command.Parameters.AddWithValue("@supplier", row.Supplier);
    command.Parameters.AddWithValue("@goods", row.Goods);
    command.Parameters.AddWithValue("@vat", row.Vat);
    command.Parameters.AddWithValue("@total", row.Total);
}

static async Task DeleteMigratedRowsAsync(
    MySqlConnection connection,
    MySqlTransaction transaction,
    IReadOnlyList<HeaderRow> headers)
{
    foreach (var header in headers)
    {
        await using var command = new MySqlCommand(
            "DELETE FROM caricorg WHERE Anno=@year AND Codice=@code;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@year", header.Year);
        command.Parameters.AddWithValue("@code", header.Code);
        await command.ExecuteNonQueryAsync();
    }
}

static async Task InsertRowAsync(
    MySqlConnection connection,
    MySqlTransaction transaction,
    int id,
    DetailRow row)
{
    await using var command = new MySqlCommand(
        """
        INSERT INTO caricorg
            (ID, Anno, Codice, Riga, Fornitore, DataDoc, Articolo, Ums,
             Quantita, Prezzo, Sconto, AliqIva, Importo, Tara, PrNetto, PrIvato)
        VALUES
            (@id, @year, @code, @row, @supplier, @date, @article, @unit,
             @quantity, @price, @discount, @vatRate, @amount, @tare, @netPrice, @vatPrice);
        """,
        connection,
        transaction);
    command.Parameters.AddWithValue("@id", id);
    command.Parameters.AddWithValue("@year", row.Year);
    command.Parameters.AddWithValue("@code", row.Code);
    command.Parameters.AddWithValue("@row", row.RowNumber);
    command.Parameters.AddWithValue("@supplier", row.Supplier);
    command.Parameters.Add("@date", MySqlDbType.Date).Value =
        row.DocumentDate.ToDateTime(TimeOnly.MinValue);
    command.Parameters.AddWithValue("@article", row.Article);
    command.Parameters.AddWithValue("@unit", row.UnitMeasure);
    command.Parameters.AddWithValue("@quantity", row.Quantity);
    command.Parameters.AddWithValue("@price", row.Price);
    command.Parameters.AddWithValue("@discount", row.Discount);
    command.Parameters.AddWithValue("@vatRate", row.VatRate);
    command.Parameters.AddWithValue("@amount", row.Amount);
    command.Parameters.AddWithValue("@tare", row.Tare);
    command.Parameters.AddWithValue("@netPrice", row.NetPrice);
    command.Parameters.AddWithValue("@vatPrice", row.VatIncludedPrice);
    await command.ExecuteNonQueryAsync();
}

static async Task RebuildParentIdsAsync(
    MySqlConnection connection,
    MySqlTransaction transaction)
{
    await using var command = new MySqlCommand(
        """
        UPDATE caricorg rg
        JOIN carico c ON c.Anno=rg.Anno AND c.Codice=rg.Codice
        SET rg.ID=c.ID
        WHERE rg.ID<>c.ID;
        """,
        connection,
        transaction);
    await command.ExecuteNonQueryAsync();
}

static async Task EnsureIndexesAsync(MySqlConnection connection)
{
    await AddIndexIfMissingAsync(
        connection,
        "carico",
        "UX_carico_Anno_Codice",
        "UNIQUE KEY `UX_carico_Anno_Codice` (`Anno`,`Codice`)");
    await AddIndexIfMissingAsync(
        connection,
        "caricorg",
        "UX_caricorg_Anno_Codice_Riga",
        "UNIQUE KEY `UX_caricorg_Anno_Codice_Riga` (`Anno`,`Codice`,`Riga`)");
    await AddIndexIfMissingAsync(
        connection,
        "caricorg",
        "IX_caricorg_ID",
        "KEY `IX_caricorg_ID` (`ID`)");
}

static async Task AddIndexIfMissingAsync(
    MySqlConnection connection,
    string table,
    string index,
    string definition)
{
    await using var exists = new MySqlCommand(
        """
        SELECT COUNT(*)
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@table AND INDEX_NAME=@index;
        """,
        connection);
    exists.Parameters.AddWithValue("@table", table);
    exists.Parameters.AddWithValue("@index", index);
    if (Convert.ToInt32(await exists.ExecuteScalarAsync()) != 0) return;
    await ExecuteAsync(connection, $"ALTER TABLE `{table}` ADD {definition};");
}

static async Task VerifyAsync(
    MySqlConnection connection,
    IReadOnlyList<HeaderRow> headers,
    IReadOnlyList<DetailRow> rows)
{
    var migratedHeaders = await ScalarLongAsync(
        connection,
        "SELECT COUNT(*) FROM carico WHERE (Anno, Codice) IN " +
        "(SELECT DISTINCT Anno, Codice FROM caricorg);");
    var mismatchedIds = await ScalarLongAsync(
        connection,
        """
        SELECT COUNT(*) FROM caricorg rg
        JOIN carico c ON c.Anno=rg.Anno AND c.Codice=rg.Codice
        WHERE rg.ID<>c.ID;
        """);
    var orphanRows = await ScalarLongAsync(
        connection,
        """
        SELECT COUNT(*) FROM caricorg rg
        LEFT JOIN carico c ON c.Anno=rg.Anno AND c.Codice=rg.Codice
        WHERE c.ID IS NULL;
        """);

    if (mismatchedIds != 0 || orphanRows != 0)
    {
        throw new InvalidOperationException(
            $"Verifica fallita: ID discordanti={mismatchedIds}, righe orfane={orphanRows}.");
    }

    var targetRows = new Dictionary<(int Year, int Code, int Row), DetailRow>();
    await using (var command = new MySqlCommand(
        """
        SELECT Anno, Codice, Riga, Fornitore, DataDoc, Articolo, Ums,
               Quantita, Prezzo, Sconto, AliqIva, Importo, Tara, PrNetto, PrIvato
        FROM caricorg;
        """,
        connection))
    await using (var reader = await command.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            var row = new DetailRow(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                DateOnly.FromDateTime(reader.GetDateTime(4)),
                reader.IsDBNull(5) ? "" : reader.GetString(5),
                reader.IsDBNull(6) ? "" : reader.GetString(6),
                reader.GetDecimal(7),
                reader.GetDecimal(8),
                reader.GetDecimal(9),
                reader.GetDecimal(10),
                reader.GetDecimal(11),
                reader.GetDecimal(12),
                reader.GetDecimal(13),
                reader.GetDecimal(14));
            targetRows[(row.Year, row.Code, row.RowNumber)] = row;
        }
    }

    foreach (var sourceRow in rows)
    {
        if (!targetRows.TryGetValue(
                (sourceRow.Year, sourceRow.Code, sourceRow.RowNumber),
                out var targetRow)
            || sourceRow != targetRow)
        {
            throw new InvalidOperationException(
                $"Riga non concorde: {sourceRow.Year}/{sourceRow.Code}/{sourceRow.RowNumber}.");
        }
    }

    foreach (var header in headers)
    {
        await using var command = new MySqlCommand(
            """
            SELECT NumDoc, DataDoc, Fornitore, Merce, Iva, Totale
            FROM carico WHERE Anno=@year AND Codice=@code LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("@year", header.Year);
        command.Parameters.AddWithValue("@code", header.Code);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("Testata migrata non trovata.");
        if ((reader.GetString(0) ?? "") != header.DocumentNumber
            || DateOnly.FromDateTime(reader.GetDateTime(1)) != header.DocumentDate
            || reader.GetInt32(2) != header.Supplier
            || reader.GetDecimal(3) != header.Goods
            || reader.GetDecimal(4) != header.Vat
            || reader.GetDecimal(5) != header.Total)
        {
            throw new InvalidOperationException(
                $"Testata non concorde: {header.Year}/{header.Code}.");
        }
    }

    var migratedRowCount = 0L;
    foreach (var header in headers)
    {
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM caricorg WHERE Anno=@year AND Codice=@code;",
            connection);
        command.Parameters.AddWithValue("@year", header.Year);
        command.Parameters.AddWithValue("@code", header.Code);
        migratedRowCount += Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    if (migratedRowCount != rows.Count)
    {
        throw new InvalidOperationException(
            $"Numero righe non concorde: CE={rows.Count}, MySQL={migratedRowCount}.");
    }

    Console.WriteLine(
        $"Verifica: {headers.Count} testate e {migratedRowCount} righe concordanti; " +
        $"ID discordanti 0; righe orfane 0.");
    _ = migratedHeaders;
}

static async Task ExecuteAsync(MySqlConnection connection, string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}

static async Task<long> ScalarLongAsync(MySqlConnection connection, string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}

internal sealed class SecretsMarker;

internal sealed record HeaderRow(
    int Year,
    int Code,
    string DocumentNumber,
    DateOnly DocumentDate,
    int Supplier,
    decimal Goods,
    decimal Vat,
    decimal Total);

internal sealed record DetailRow(
    int Year,
    int Code,
    int RowNumber,
    int Supplier,
    DateOnly DocumentDate,
    string Article,
    string UnitMeasure,
    decimal Quantity,
    decimal Price,
    decimal Discount,
    decimal VatRate,
    decimal Amount,
    decimal Tare,
    decimal NetPrice,
    decimal VatIncludedPrice);
