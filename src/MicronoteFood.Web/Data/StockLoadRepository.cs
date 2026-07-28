using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class StockLoadRepository(MicronoteDb database)
{
    private const int StockLoadPurchaseCause = 10;
    private const int StockLoadReturnCause = 12;
    private const int StockLoadSector = 10;

    public async Task<StockLoadEditDocument?> GetEditAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return null;
        }

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT COALESCE(c.ID, 0) AS ID,
                   COALESCE(c.Anno, 0) AS Anno,
                   COALESCE(c.Codice, 0) AS Codice,
                   COALESCE(c.Causale, 10) AS Causale,
                   COALESCE(c.NumDoc, '') AS NumDoc,
                   c.DataDoc,
                   COALESCE(c.Fornitore, 0) AS Fornitore,
                   COALESCE(f.Nome, '') AS FornitoreNome,
                   COALESCE(c.PuntoV, 0) AS PuntoV,
                   COALESCE(c.FeName, '') AS FeName
            FROM carico c
            LEFT JOIN fornitori f ON f.Codice = c.Fornitore
            WHERE c.ID = @id
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var document = new StockLoadEditDocument
        {
            Id = Convert.ToInt32(reader["ID"]),
            Year = Convert.ToInt32(reader["Anno"]),
            Code = Convert.ToInt32(reader["Codice"]),
            CauseCode = Convert.ToInt32(reader["Causale"]),
            DocumentNumber = Convert.ToString(reader["NumDoc"]) ?? "",
            DocumentDate = reader["DataDoc"] == DBNull.Value
                ? null
                : DateOnly.FromDateTime(Convert.ToDateTime(reader["DataDoc"])),
            SupplierCode = Convert.ToInt32(reader["Fornitore"]),
            SupplierName = Convert.ToString(reader["FornitoreNome"]) ?? "",
            StoreCode = Convert.ToInt32(reader["PuntoV"]),
            ElectronicInvoiceName = Convert.ToString(reader["FeName"]) ?? ""
        };
        await reader.DisposeAsync();

        document.Details = await ListDetailsForDocumentAsync(
            connection,
            document.Id,
            document.Year,
            document.Code,
            cancellationToken);
        return document;
    }

    public async Task<IReadOnlyList<StockLoadDetailItem>> GetDetailsAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return [];
        }

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return await ListDetailsByIdAsync(connection, id, cancellationToken);
    }

    public async Task<StockLoadSaveResult> SaveAsync(
        StockLoadSaveCommand command,
        CancellationToken cancellationToken = default)
    {
        command.CauseCode = command.CauseCode == 0 ? StockLoadPurchaseCause : command.CauseCode;
        var normalizedRows = NormalizeSaveRows(command.Rows);
        var goods = normalizedRows.Sum(row => row.Amount);
        var vat = normalizedRows.Sum(row => Math.Round(row.Amount * row.VatRate / 100m, 2, MidpointRounding.AwayFromZero));
        var total = goods + vat;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var existingById = command.Id > 0
                ? await ExistingDocumentAsync(connection, transaction, command.Id, cancellationToken)
                : null;
            var duplicateDocument = await DuplicateDocumentAsync(
                connection,
                transaction,
                command,
                cancellationToken);
            if (command.Id > 0 && existingById is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new StockLoadSaveResult(
                    false,
                    "Documento di carico non trovato.",
                    command.Id,
                    command.Year,
                    command.Code,
                    false,
                    goods,
                    vat,
                    total);
            }

            if (duplicateDocument is not null && existingById is not null && duplicateDocument.Id != existingById.Id)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new StockLoadSaveResult(
                    false,
                    $"Documento gia' registrato nella partita {duplicateDocument.Code:000000}.",
                    command.Id,
                    command.Year,
                    command.Code,
                    false,
                    goods,
                    vat,
                    total);
            }

            if (duplicateDocument is not null && existingById is null && !command.AllowOverwrite)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new StockLoadSaveResult(
                    false,
                    $"Documento gia' registrato nella partita {duplicateDocument.Code:000000}.",
                    duplicateDocument.Id,
                    duplicateDocument.Year,
                    duplicateDocument.Code,
                    false,
                    goods,
                    vat,
                    total,
                    true);
            }

            var existing = existingById ?? duplicateDocument;
            var overwritten = command.Id <= 0 && duplicateDocument is not null;
            var code = existing?.Code
                ?? (command.Code > 0 ? command.Code : await NextCodeAsync(connection, transaction, command.Year, cancellationToken));
            var previousYear = existing?.Year ?? command.Year;
            var id = existing?.Id
                ?? await InsertDocumentAsync(
                    connection,
                    transaction,
                    command,
                    code,
                    goods,
                    vat,
                    total,
                    cancellationToken);

            if (existing is not null)
            {
                command.Id = id;
                await UpdateDocumentAsync(
                    connection,
                    transaction,
                    command,
                    id,
                    code,
                    goods,
                    vat,
                    total,
                    cancellationToken);
            }

            await DeleteDetailRowsAsync(connection, transaction, id, previousYear, command.Year, code, cancellationToken);
            await InsertDetailRowsAsync(connection, transaction, command, id, code, normalizedRows, cancellationToken);
            await DeleteStockMovementsAsync(connection, transaction, previousYear, command.Year, code, cancellationToken);
            await InsertStockMovementsAsync(connection, transaction, command, code, normalizedRows, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new StockLoadSaveResult(true, "", id, command.Year, code, overwritten, goods, vat, total);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<StockLoadSaveResult> CheckOverwriteAsync(
        StockLoadSaveCommand command,
        CancellationToken cancellationToken = default)
    {
        command.CauseCode = command.CauseCode == 0 ? StockLoadPurchaseCause : command.CauseCode;
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var existingById = command.Id > 0
            ? await ExistingDocumentAsync(connection, null, command.Id, cancellationToken)
            : null;
        var duplicateDocument = await DuplicateDocumentAsync(connection, null, command, cancellationToken);

        if (command.Id > 0 && existingById is null)
        {
            return new StockLoadSaveResult(
                false,
                "Documento di carico non trovato.",
                command.Id,
                command.Year,
                command.Code,
                false,
                0,
                0,
                0);
        }

        if (duplicateDocument is not null && existingById is not null && duplicateDocument.Id != existingById.Id)
        {
            return new StockLoadSaveResult(
                false,
                $"Documento gia' registrato nella partita {duplicateDocument.Code:000000}.",
                command.Id,
                command.Year,
                command.Code,
                false,
                0,
                0,
                0);
        }

        if (duplicateDocument is not null && existingById is null)
        {
            return new StockLoadSaveResult(
                false,
                $"Documento gia' registrato nella partita {duplicateDocument.Code:000000}.",
                duplicateDocument.Id,
                duplicateDocument.Year,
                duplicateDocument.Code,
                false,
                0,
                0,
                0,
                true);
        }

        return new StockLoadSaveResult(true, "", command.Id, command.Year, command.Code, false, 0, 0, 0);
    }

    public async Task<StockLoadListPageModel> GetListAsync(
        int year,
        int? month,
        int? supplierCode,
        int? storeCode,
        string? articleCode,
        string? selectedKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedArticle = articleCode?.Trim() ?? "";
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var documents = await ListDocumentsAsync(
            connection,
            year,
            month,
            supplierCode,
            storeCode,
            normalizedArticle,
            cancellationToken);
        var selected = SelectDocument(documents, selectedKey);

        return new StockLoadListPageModel
        {
            Year = year,
            Month = month,
            SupplierCode = supplierCode,
            SupplierName = supplierCode is null
                ? ""
                : await SupplierNameAsync(connection, supplierCode.Value, cancellationToken),
            StoreCode = storeCode,
            ArticleCode = normalizedArticle,
            ArticleDescription = string.IsNullOrWhiteSpace(normalizedArticle)
                ? ""
                : await ArticleDescriptionAsync(connection, normalizedArticle, cancellationToken),
            SelectedKey = selected?.Key ?? "",
            Documents = documents,
            Details = selected is null
                ? []
                : await ListDetailsAsync(connection, selected, cancellationToken),
            Totals = TotalsFrom(documents),
            Years = await ListYearsAsync(connection, year, cancellationToken),
            Months = MonthOptions,
            Suppliers = await ListSuppliersAsync(connection, cancellationToken),
            Stores = await ListStoresAsync(connection, cancellationToken)
        };
    }

    private static StockLoadListItem? SelectDocument(
        IReadOnlyList<StockLoadListItem> documents,
        string? selectedKey) =>
        string.IsNullOrWhiteSpace(selectedKey)
            ? documents.FirstOrDefault()
            : documents.FirstOrDefault(document => document.Key == selectedKey)
              ?? documents.FirstOrDefault();

    private sealed record ExistingStockLoadDocument(int Id, int Year, int Code);

    private static IReadOnlyList<StockLoadSaveRow> NormalizeSaveRows(IReadOnlyList<StockLoadSaveRow> rows)
    {
        var rowNumber = 0;
        return rows
            .Where(row => !string.IsNullOrWhiteSpace(row.ArticleCode))
            .Select(row => row with
            {
                RowNumber = ++rowNumber,
                ArticleCode = row.ArticleCode.Trim(),
                Description = row.Description.Trim(),
                UnitMeasure = row.UnitMeasure.Trim(),
                Quantity = Decimal3(row.Quantity),
                Price = Decimal3(row.Price),
                Discount = Decimal2(row.Discount),
                Amount = Decimal2(row.Amount),
                VatRate = Decimal2(row.VatRate),
                Tare = Decimal3(row.Tare),
                NetPrice = NormalizeNetPrice(row),
                VatIncludedPrice = NormalizeVatIncludedPrice(row)
            })
            .ToArray();
    }

    private static async Task<ExistingStockLoadDocument?> ExistingDocumentAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        int id,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT ID, Anno, Codice
            FROM carico
            WHERE ID = @id
            LIMIT 1;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ExistingStockLoadDocument(
                Convert.ToInt32(reader["ID"]),
                Convert.ToInt32(reader["Anno"]),
                Convert.ToInt32(reader["Codice"]))
            : null;
    }

    private static async Task<ExistingStockLoadDocument?> DuplicateDocumentAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        StockLoadSaveCommand command,
        CancellationToken cancellationToken)
    {
        await using var sql = new MySqlCommand(
            """
            SELECT COALESCE(ID, 0) AS ID,
                   COALESCE(Anno, 0) AS Anno,
                   COALESCE(Codice, 0) AS Codice
            FROM carico
            WHERE Fornitore = @supplierCode
              AND UPPER(TRIM(COALESCE(NumDoc, ''))) = @documentNumber
              AND DataDoc = @documentDate
              AND (@id = 0 OR ID <> @id)
            LIMIT 1;
            """,
            connection,
            transaction);
        sql.Parameters.AddWithValue("@supplierCode", command.SupplierCode);
        sql.Parameters.AddWithValue("@documentNumber", command.DocumentNumber.Trim().ToUpperInvariant());
        sql.Parameters.Add("@documentDate", MySqlDbType.Date).Value = command.DocumentDate!.Value.ToDateTime(TimeOnly.MinValue);
        sql.Parameters.AddWithValue("@id", command.Id);

        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ExistingStockLoadDocument(
                Convert.ToInt32(reader["ID"]),
                Convert.ToInt32(reader["Anno"]),
                Convert.ToInt32(reader["Codice"]))
            : null;
    }

    private static async Task<int> NextCodeAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT COALESCE(MAX(Codice), 0) + 1
            FROM carico
            WHERE Anno = @year;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@year", year);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> InsertDocumentAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        StockLoadSaveCommand command,
        int code,
        decimal goods,
        decimal vat,
        decimal total,
        CancellationToken cancellationToken)
    {
        await using var sql = new MySqlCommand(
            """
            INSERT INTO carico
                (Anno, Codice, NumDoc, DataDoc, Causale, Fornitore, Merce, Iva, Totale, FeName, PuntoV)
            VALUES
                (@year, @code, @documentNumber, @documentDate, @cause, @supplierCode, @goods, @vat, @total, @fileName, @storeCode);
            """,
            connection,
            transaction);
        AddDocumentParameters(sql, command, code, goods, vat, total);
        await sql.ExecuteNonQueryAsync(cancellationToken);
        return Convert.ToInt32(sql.LastInsertedId);
    }

    private static async Task UpdateDocumentAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        StockLoadSaveCommand command,
        int id,
        int code,
        decimal goods,
        decimal vat,
        decimal total,
        CancellationToken cancellationToken)
    {
        await using var sql = new MySqlCommand(
            """
            UPDATE carico
            SET Anno = @year,
                Codice = @code,
                NumDoc = @documentNumber,
                DataDoc = @documentDate,
                Causale = @cause,
                Fornitore = @supplierCode,
                Merce = @goods,
                Iva = @vat,
                Totale = @total,
                FeName = @fileName,
                PuntoV = @storeCode
            WHERE ID = @id;
            """,
            connection,
            transaction);
        AddDocumentParameters(sql, command, code, goods, vat, total);
        sql.Parameters.AddWithValue("@id", id);
        await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddDocumentParameters(
        MySqlCommand command,
        StockLoadSaveCommand stockLoad,
        int code,
        decimal goods,
        decimal vat,
        decimal total)
    {
        command.Parameters.AddWithValue("@year", stockLoad.Year);
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@documentNumber", stockLoad.DocumentNumber.Trim());
        command.Parameters.Add("@documentDate", MySqlDbType.Date).Value = stockLoad.DocumentDate!.Value.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@cause", stockLoad.CauseCode);
        command.Parameters.AddWithValue("@supplierCode", stockLoad.SupplierCode);
        command.Parameters.AddWithValue("@goods", goods);
        command.Parameters.AddWithValue("@vat", vat);
        command.Parameters.AddWithValue("@total", total);
        command.Parameters.AddWithValue("@fileName", stockLoad.ElectronicInvoiceName.Trim());
        command.Parameters.AddWithValue("@storeCode", stockLoad.StoreCode);
    }

    private static async Task DeleteDetailRowsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int id,
        int previousYear,
        int currentYear,
        int code,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            DELETE FROM caricorg
            WHERE ID = @id
               OR (Anno = @previousYear AND Codice = @code)
               OR (Anno = @currentYear AND Codice = @code);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@previousYear", previousYear);
        command.Parameters.AddWithValue("@currentYear", currentYear);
        command.Parameters.AddWithValue("@code", code);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertDetailRowsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        StockLoadSaveCommand command,
        int id,
        int code,
        IReadOnlyList<StockLoadSaveRow> rows,
        CancellationToken cancellationToken)
    {
        foreach (var row in rows)
        {
            await using var sql = new MySqlCommand(
                """
                INSERT INTO caricorg
                    (ID, Anno, Codice, Riga, DataDoc, Articolo, Fornitore, Ums,
                     Quantita, Prezzo, Sconto, AliqIva, Importo, Tara, PrNetto, PrIvato)
                VALUES
                    (@id, @year, @code, @rowNumber, @documentDate, @articleCode, @supplierCode, @unitMeasure,
                     @quantity, @price, @discount, @vatRate, @amount, @tare, @netPrice, @vatIncludedPrice);
                """,
                connection,
                transaction);
            AddRowParameters(sql, command, id, code, row);
            await sql.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task DeleteStockMovementsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int previousYear,
        int currentYear,
        int code,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            DELETE FROM movimenti
            WHERE Anno IN (@previousYear, @currentYear)
              AND Settore = @sector
              AND Codice = @code;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@previousYear", previousYear);
        command.Parameters.AddWithValue("@currentYear", currentYear);
        command.Parameters.AddWithValue("@sector", StockLoadSector);
        command.Parameters.AddWithValue("@code", code);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertStockMovementsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        StockLoadSaveCommand command,
        int code,
        IReadOnlyList<StockLoadSaveRow> rows,
        CancellationToken cancellationToken)
    {
        foreach (var row in rows)
        {
            await using var sql = new MySqlCommand(
                """
                INSERT INTO movimenti
                    (Anno, Settore, Codice, Riga, Causale, NumDoc, DataMov, CliFor, Ditta, TipoMov,
                     Articolo, Quantita, Prezzo, Importo, PuntoV)
                VALUES
                    (@year, @sector, @code, @rowNumber, @cause, @documentNumber, @documentDate,
                     @subjectType, @supplierCode, @movementType, @articleCode, @quantity,
                     @netPrice, @amount, @storeCode);
                """,
                connection,
                transaction);
            sql.Parameters.AddWithValue("@year", command.Year);
            sql.Parameters.AddWithValue("@sector", StockLoadSector);
            sql.Parameters.AddWithValue("@code", code);
            sql.Parameters.AddWithValue("@rowNumber", row.RowNumber);
            sql.Parameters.AddWithValue("@cause", command.CauseCode);
            sql.Parameters.AddWithValue("@documentNumber", command.DocumentNumber.Trim());
            sql.Parameters.Add("@documentDate", MySqlDbType.Date).Value = command.DocumentDate!.Value.ToDateTime(TimeOnly.MinValue);
            sql.Parameters.AddWithValue("@subjectType", SubjectTypeForCause(command.CauseCode));
            sql.Parameters.AddWithValue("@supplierCode", command.SupplierCode);
            sql.Parameters.AddWithValue("@movementType", "C");
            sql.Parameters.AddWithValue("@articleCode", row.ArticleCode);
            sql.Parameters.AddWithValue("@quantity", row.Quantity);
            sql.Parameters.AddWithValue("@netPrice", row.NetPrice);
            sql.Parameters.AddWithValue("@amount", row.Amount);
            sql.Parameters.AddWithValue("@storeCode", command.StoreCode);
            await sql.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static void AddRowParameters(
        MySqlCommand command,
        StockLoadSaveCommand stockLoad,
        int id,
        int code,
        StockLoadSaveRow row)
    {
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@year", stockLoad.Year);
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@rowNumber", row.RowNumber);
        command.Parameters.Add("@documentDate", MySqlDbType.Date).Value = stockLoad.DocumentDate!.Value.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@articleCode", row.ArticleCode);
        command.Parameters.AddWithValue("@supplierCode", stockLoad.SupplierCode);
        command.Parameters.AddWithValue("@unitMeasure", row.UnitMeasure);
        command.Parameters.AddWithValue("@quantity", row.Quantity);
        command.Parameters.AddWithValue("@price", row.Price);
        command.Parameters.AddWithValue("@discount", row.Discount);
        command.Parameters.AddWithValue("@vatRate", row.VatRate);
        command.Parameters.AddWithValue("@amount", row.Amount);
        command.Parameters.AddWithValue("@tare", row.Tare);
        command.Parameters.AddWithValue("@netPrice", row.NetPrice);
        command.Parameters.AddWithValue("@vatIncludedPrice", row.VatIncludedPrice);
    }

    private static decimal Decimal3(decimal value) =>
        Math.Round(value, 3, MidpointRounding.AwayFromZero);

    private static decimal Decimal2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal NormalizeNetPrice(StockLoadSaveRow row) =>
        Decimal3(row.NetPrice == 0m && row.Price != 0m
            ? row.Price * (1m - row.Discount / 100m)
            : row.NetPrice);

    private static decimal NormalizeVatIncludedPrice(StockLoadSaveRow row)
    {
        if (row.VatIncludedPrice != 0m || row.Price == 0m)
        {
            return Decimal3(row.VatIncludedPrice);
        }

        var netPrice = NormalizeNetPrice(row);
        return Decimal3(netPrice * (1m + row.VatRate / 100m));
    }

    private static string SubjectTypeForCause(int causeCode) =>
        causeCode == StockLoadReturnCause ? "C" : "F";

    private static async Task<IReadOnlyList<StockLoadListItem>> ListDocumentsAsync(
        MySqlConnection connection,
        int year,
        int? month,
        int? supplierCode,
        int? storeCode,
        string articleCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT COALESCE(c.ID, 0) AS ID,
                   CASE WHEN COALESCE(c.ID, 0) = 0 THEN 0 ELSE 1 END AS HasId,
                   c.Anno,
                   COALESCE(c.Codice, 0) AS Codice,
                   COALESCE(c.NumDoc, '') AS NumDoc,
                   COALESCE(c.DataDoc, '1900-01-01') AS DataDoc,
                   COALESCE(c.Fornitore, 0) AS Fornitore,
                   COALESCE(f.Nome, '') AS FornitoreNome,
                   COALESCE(c.Merce, 0) AS Merce,
                   COALESCE(c.Iva, 0) AS Iva,
                   COALESCE(c.Totale, 0) AS Totale,
                   COALESCE(c.FeName, '') AS FeName
            FROM carico c
            LEFT JOIN fornitori f ON f.Codice = c.Fornitore
            LEFT JOIN caricorg rg ON rg.ID = c.ID
            WHERE c.Anno = @year
              AND (@month IS NULL OR MONTH(c.DataDoc) = @month)
              AND (@supplierCode IS NULL OR c.Fornitore = @supplierCode)
              AND (
                  @storeCode IS NULL
                  OR (@storeCode = 0 AND COALESCE(c.PuntoV, 0) = 0)
                  OR c.PuntoV = @storeCode
              )
              AND (@articleCode = '' OR rg.Articolo = @articleCode)
            ORDER BY DataDoc DESC, NumDoc DESC, Codice DESC;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@month", month is null ? DBNull.Value : month.Value);
        command.Parameters.AddWithValue("@supplierCode", supplierCode is null ? DBNull.Value : supplierCode.Value);
        command.Parameters.AddWithValue("@storeCode", storeCode is null ? DBNull.Value : storeCode.Value);
        command.Parameters.AddWithValue("@articleCode", articleCode);

        var rows = new List<StockLoadListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = Convert.ToInt32(reader["ID"]);
            var hasId = Convert.ToInt32(reader["HasId"]) == 1;
            var documentYear = Convert.ToInt32(reader["Anno"]);
            var documentCode = Convert.ToInt32(reader["Codice"]);
            rows.Add(new StockLoadListItem(
                hasId ? $"id:{id}" : $"legacy:{documentYear}:{documentCode}",
                id,
                hasId,
                documentYear,
                documentCode,
                Convert.ToString(reader["NumDoc"]) ?? "",
                DateOnly.FromDateTime(Convert.ToDateTime(reader["DataDoc"])),
                Convert.ToInt32(reader["Fornitore"]),
                Convert.ToString(reader["FornitoreNome"]) ?? "",
                Money(reader["Merce"]),
                Money(reader["Iva"]),
                Money(reader["Totale"]),
                Convert.ToString(reader["FeName"]) ?? ""));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<StockLoadDetailItem>> ListDetailsAsync(
        MySqlConnection connection,
        StockLoadListItem document,
        CancellationToken cancellationToken)
    {
        var sql = document.HasId
            ? """
            SELECT COALESCE(rg.Riga, 0) AS Riga,
                   COALESCE(rg.Articolo, '') AS Articolo,
                   COALESCE(a.Descrizione, '') AS Descrizione,
                   COALESCE(rg.Ums, '') AS Ums,
                   COALESCE(rg.Quantita, 0) AS Quantita,
                   COALESCE(rg.Prezzo, 0) AS Prezzo,
                   COALESCE(rg.Sconto, 0) AS Sconto,
                   COALESCE(rg.Importo, 0) AS Importo,
                   COALESCE(rg.AliqIva, 0) AS AliqIva,
                   COALESCE(rg.Tara, 0) AS Tara,
                   COALESCE(rg.PrNetto, 0) AS PrNetto,
                   COALESCE(rg.PrIvato, 0) AS PrIvato
            FROM caricorg rg
            LEFT JOIN articoli a ON a.Codice = rg.Articolo
            WHERE rg.ID = @id
            ORDER BY rg.Riga;
            """
            : """
            SELECT COALESCE(rg.Riga, 0) AS Riga,
                   COALESCE(rg.Articolo, '') AS Articolo,
                   COALESCE(a.Descrizione, '') AS Descrizione,
                   COALESCE(rg.Ums, '') AS Ums,
                   COALESCE(rg.Quantita, 0) AS Quantita,
                   COALESCE(rg.Prezzo, 0) AS Prezzo,
                   COALESCE(rg.Sconto, 0) AS Sconto,
                   COALESCE(rg.Importo, 0) AS Importo,
                   COALESCE(rg.AliqIva, 0) AS AliqIva,
                   COALESCE(rg.Tara, 0) AS Tara,
                   COALESCE(rg.PrNetto, 0) AS PrNetto,
                   COALESCE(rg.PrIvato, 0) AS PrIvato
            FROM caricorg rg
            LEFT JOIN articoli a ON a.Codice = rg.Articolo
            WHERE rg.Anno = @year
              AND rg.Codice = @code
            ORDER BY rg.Riga;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", document.Id);
        command.Parameters.AddWithValue("@year", document.Year);
        command.Parameters.AddWithValue("@code", document.Code);

        var rows = new List<StockLoadDetailItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new StockLoadDetailItem(
                Convert.ToInt32(reader["Riga"]),
                Convert.ToString(reader["Articolo"]) ?? "",
                Convert.ToString(reader["Descrizione"]) ?? "",
                Convert.ToString(reader["Ums"]) ?? "",
                Money(reader["Quantita"]),
                Money(reader["Prezzo"]),
                Money(reader["Sconto"]),
                Money(reader["Importo"]),
                Money(reader["AliqIva"]),
                Money(reader["Tara"]),
                Money(reader["PrNetto"]),
                Money(reader["PrIvato"])));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<StockLoadDetailItem>> ListDetailsByIdAsync(
        MySqlConnection connection,
        int id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(rg.Riga, 0) AS Riga,
                   COALESCE(rg.Articolo, '') AS Articolo,
                   COALESCE(a.Descrizione, '') AS Descrizione,
                   COALESCE(rg.Ums, '') AS Ums,
                   COALESCE(rg.Quantita, 0) AS Quantita,
                   COALESCE(rg.Prezzo, 0) AS Prezzo,
                   COALESCE(rg.Sconto, 0) AS Sconto,
                   COALESCE(rg.Importo, 0) AS Importo,
                   COALESCE(rg.AliqIva, 0) AS AliqIva,
                   COALESCE(rg.Tara, 0) AS Tara,
                   COALESCE(rg.PrNetto, 0) AS PrNetto,
                   COALESCE(rg.PrIvato, 0) AS PrIvato
            FROM caricorg rg
            LEFT JOIN articoli a ON a.Codice = rg.Articolo
            WHERE rg.ID = @id
            ORDER BY rg.Riga;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        return await ReadDetailItemsAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<StockLoadDetailItem>> ListDetailsForDocumentAsync(
        MySqlConnection connection,
        int id,
        int year,
        int code,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(rg.Riga, 0) AS Riga,
                   COALESCE(rg.Articolo, '') AS Articolo,
                   COALESCE(a.Descrizione, '') AS Descrizione,
                   COALESCE(rg.Ums, '') AS Ums,
                   COALESCE(rg.Quantita, 0) AS Quantita,
                   COALESCE(rg.Prezzo, 0) AS Prezzo,
                   COALESCE(rg.Sconto, 0) AS Sconto,
                   COALESCE(rg.Importo, 0) AS Importo,
                   COALESCE(rg.AliqIva, 0) AS AliqIva,
                   COALESCE(rg.Tara, 0) AS Tara,
                   COALESCE(rg.PrNetto, 0) AS PrNetto,
                   COALESCE(rg.PrIvato, 0) AS PrIvato
            FROM caricorg rg
            LEFT JOIN articoli a ON a.Codice = rg.Articolo
            WHERE (COALESCE(rg.ID, 0) = @id AND @id > 0)
               OR (rg.Anno = @year AND rg.Codice = @code)
            GROUP BY COALESCE(rg.Riga, 0),
                     COALESCE(rg.Articolo, ''),
                     COALESCE(a.Descrizione, ''),
                     COALESCE(rg.Ums, ''),
                     COALESCE(rg.Quantita, 0),
                     COALESCE(rg.Prezzo, 0),
                     COALESCE(rg.Sconto, 0),
                     COALESCE(rg.Importo, 0),
                     COALESCE(rg.AliqIva, 0),
                     COALESCE(rg.Tara, 0),
                     COALESCE(rg.PrNetto, 0),
                     COALESCE(rg.PrIvato, 0)
            ORDER BY Riga;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@code", code);
        return await ReadDetailItemsAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<StockLoadDetailItem>> ReadDetailItemsAsync(
        MySqlCommand command,
        CancellationToken cancellationToken)
    {
        var rows = new List<StockLoadDetailItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new StockLoadDetailItem(
                Convert.ToInt32(reader["Riga"]),
                Convert.ToString(reader["Articolo"]) ?? "",
                Convert.ToString(reader["Descrizione"]) ?? "",
                Convert.ToString(reader["Ums"]) ?? "",
                Money(reader["Quantita"]),
                Money(reader["Prezzo"]),
                Money(reader["Sconto"]),
                Money(reader["Importo"]),
                Money(reader["AliqIva"]),
                Money(reader["Tara"]),
                Money(reader["PrNetto"]),
                Money(reader["PrIvato"])));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<int>> ListYearsAsync(
        MySqlConnection connection,
        int currentYear,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT Anno
            FROM carico
            ORDER BY Anno DESC;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var years = new List<int>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            years.Add(Convert.ToInt32(reader["Anno"]));
        }

        if (!years.Contains(currentYear))
        {
            years.Insert(0, currentYear);
        }

        return years;
    }

    private static async Task<IReadOnlyList<PurchaseInvoiceStoreOption>> ListStoresAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Codice, COALESCE(Nome, '') AS Nome
            FROM puntivendita
            ORDER BY Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<PurchaseInvoiceStoreOption>
        {
            new(0, "Spese comuni")
        };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = Convert.ToInt32(reader["Codice"]);
            var name = Convert.ToString(reader["Nome"]) ?? "";
            rows.Add(new PurchaseInvoiceStoreOption(code, $"{code:000} - {name}"));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<StockLoadSupplierOption>> ListSuppliersAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT c.Fornitore AS Codice,
                   COALESCE(f.Nome, '') AS Nome
            FROM carico c
            LEFT JOIN fornitori f ON f.Codice = c.Fornitore
            WHERE COALESCE(c.Fornitore, 0) > 0
            ORDER BY Nome, Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<StockLoadSupplierOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new StockLoadSupplierOption(
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Nome"]) ?? ""));
        }

        return rows;
    }

    private static async Task<string> SupplierNameAsync(
        MySqlConnection connection,
        int code,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COALESCE(Nome, '') FROM fornitori WHERE Codice = @code LIMIT 1;",
            connection);
        command.Parameters.AddWithValue("@code", code);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? "";
    }

    private static async Task<string> ArticleDescriptionAsync(
        MySqlConnection connection,
        string code,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COALESCE(Descrizione, '') FROM articoli WHERE Codice = @code LIMIT 1;",
            connection);
        command.Parameters.AddWithValue("@code", code);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? "";
    }

    private static IReadOnlyList<PurchaseInvoiceMonthOption> MonthOptions { get; } =
    [
        new(1, "Gennaio"),
        new(2, "Febbraio"),
        new(3, "Marzo"),
        new(4, "Aprile"),
        new(5, "Maggio"),
        new(6, "Giugno"),
        new(7, "Luglio"),
        new(8, "Agosto"),
        new(9, "Settembre"),
        new(10, "Ottobre"),
        new(11, "Novembre"),
        new(12, "Dicembre")
    ];

    private static StockLoadTotals TotalsFrom(IReadOnlyList<StockLoadListItem> rows) =>
        new(rows.Sum(row => row.Goods), rows.Sum(row => row.Vat), rows.Sum(row => row.Total));

    private static decimal Money(object value) =>
        value is null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
}
