using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class PurchaseInvoiceRepository(MicronoteDb database)
{
    private const int PurchaseInvoiceSector = 10;
    private const int SupplierAccountCode = 81;
    private const int PurchaseVatAccountCode = 83;
    private const string SupplierSubjectType = "F";

    public async Task<PurchaseInvoiceListPageModel> GetListAsync(
        int year,
        int? month,
        int? causeCode,
        int? supplierCode,
        int? storeCode,
        int? contraAccountCode,
        bool showDueDates,
        int? selectedId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var invoices = await ListInvoicesAsync(
            connection,
            year,
            month,
            causeCode,
            supplierCode,
            storeCode,
            contraAccountCode,
            cancellationToken);
        var selected = SelectInvoice(invoices, selectedId);

        return new PurchaseInvoiceListPageModel
        {
            Year = year,
            Month = month,
            CauseCode = causeCode,
            SupplierCode = supplierCode,
            SupplierName = supplierCode is null
                ? ""
                : await SupplierNameAsync(connection, supplierCode.Value, cancellationToken),
            StoreCode = storeCode,
            ContraAccountCode = contraAccountCode,
            ShowDueDates = showDueDates,
            SelectedId = selected?.Id,
            Invoices = invoices,
            Totals = TotalsFrom(invoices),
            Years = await ListYearsAsync(connection, year, cancellationToken),
            Months = MonthOptions,
            Causes = await ListCausesAsync(connection, cancellationToken),
            Stores = await ListStoresAsync(connection, cancellationToken),
            ContraAccounts = await ListContraAccountsAsync(connection, cancellationToken)
        };
    }


    public async Task<bool> DeleteByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var invoice = await ExistingInvoiceByIdAsync(connection, id, cancellationToken);
        if (invoice is null)
        {
            return false;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var movement = await ExistingAccountingMovementAsync(connection, transaction, id, cancellationToken);
            if (movement is not null)
            {
                await DeleteAccountingRowsAsync(connection, transaction, movement.Id, cancellationToken);
                await using (var linkedCommand = new MySqlCommand(
                    "DELETE FROM movcontdc WHERE Mov_Id = @id;",
                    connection,
                    transaction))
                {
                    linkedCommand.Parameters.AddWithValue("@id", movement.Id);
                    await linkedCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var movementCommand = new MySqlCommand(
                    "DELETE FROM movcont WHERE ID = @id AND Settore = @sector AND Documento = @document;",
                    connection,
                    transaction))
                {
                    movementCommand.Parameters.AddWithValue("@id", movement.Id);
                    movementCommand.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);
                    movementCommand.Parameters.AddWithValue("@document", id);
                    await movementCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await DeleteVatRowsAsync(connection, transaction, id, cancellationToken);
            await DeleteDueDatesAsync(connection, transaction, invoice.Year, invoice.Code, cancellationToken);

            await using var invoiceCommand = new MySqlCommand(
                "DELETE FROM moviva WHERE ID = @id AND Settore = @sector;",
                connection,
                transaction);
            invoiceCommand.Parameters.AddWithValue("@id", id);
            invoiceCommand.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);
            var affected = await invoiceCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return affected > 0;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
    public async Task<PurchaseInvoiceEditPageModel> GetEditMaskAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        return await BuildEditModelAsync(connection, year, cancellationToken);
    }

    public async Task<PurchaseInvoiceEditPageModel> GetEditAsync(
        int id,
        int fallbackYear,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var invoice = await BuildEditModelAsync(connection, fallbackYear, cancellationToken);

        await using var command = new MySqlCommand(
            """
            SELECT mv.ID,
                   mv.Anno,
                   mv.Codice,
                   COALESCE(mv.Causale, 0) AS Causale,
                   COALESCE(mv.NumDoc, '') AS NumDoc,
                   mv.DataDoc,
                   COALESCE(mv.Ditta, 0) AS Ditta,
                   COALESCE(f.Nome, '') AS Fornitore,
                   COALESCE(mv.CtPartita, 0) AS CtPartita,
                   COALESCE(mv.PuntoV, 0) AS PuntoV,
                   COALESCE(vat.Imponibile, 0) AS Imponibile,
                   COALESCE(vat.Iva, 0) AS Iva,
                   COALESCE(vat.Imponibile, 0) + COALESCE(vat.Iva, 0) AS Totale,
                   COALESCE(mv.Pagamento, 0) AS Pagamento,
                   COALESCE(mv.Banca, 0) AS Banca,
                   COALESCE(mv.FeName, '') AS FeName,
                   COALESCE(mv.Notes, '') AS Notes
            FROM moviva mv
            LEFT JOIN (
                SELECT ID,
                       SUM(COALESCE(Imponibile, 0)) AS Imponibile,
                       SUM(COALESCE(Iva, 0)) AS Iva
                FROM movivarg
                WHERE Settore = @sector
                GROUP BY ID
            ) vat ON vat.ID = mv.ID
            LEFT JOIN fornitori f ON f.Codice = mv.Ditta
            WHERE mv.ID = @id
              AND mv.Settore = @sector
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return invoice;
            }

            invoice.Id = Convert.ToInt32(reader["ID"]);
            invoice.Year = Convert.ToInt32(reader["Anno"]);
            invoice.Code = Convert.ToInt32(reader["Codice"]);
            invoice.CauseCode = Convert.ToInt32(reader["Causale"]);
            invoice.DocumentNumber = Convert.ToString(reader["NumDoc"]) ?? "";
            invoice.DocumentDate = reader["DataDoc"] is DateTime documentDate
                ? DateOnly.FromDateTime(documentDate)
                : DateOnly.FromDateTime(DateTime.Today);
            invoice.SupplierCode = Convert.ToInt32(reader["Ditta"]);
            invoice.SupplierName = Convert.ToString(reader["Fornitore"]) ?? "";
            invoice.ContraAccountCode = Convert.ToInt32(reader["CtPartita"]);
            invoice.StoreCode = Convert.ToInt32(reader["PuntoV"]);
            invoice.TaxableTotal = Convert.ToDecimal(reader["Imponibile"]);
            invoice.VatTotal = Convert.ToDecimal(reader["Iva"]);
            invoice.Total = Convert.ToDecimal(reader["Totale"]);
            invoice.PaymentCode = Convert.ToInt32(reader["Pagamento"]);
            invoice.BankCode = Convert.ToInt32(reader["Banca"]);
            invoice.ElectronicInvoiceFileName = Convert.ToString(reader["FeName"]) ?? "";
            invoice.Notes = Convert.ToString(reader["Notes"]) ?? "";
        }

        invoice.VatRows = await InvoiceVatRowsAsync(connection, id, cancellationToken);
        invoice.DueRows = await InvoiceDueRowsAsync(connection, id, invoice.Year, invoice.Code, cancellationToken);
        return invoice;
    }

    private static async Task<PurchaseInvoiceEditPageModel> BuildEditModelAsync(
        MySqlConnection connection,
        int year,
        CancellationToken cancellationToken)
    {
        return new PurchaseInvoiceEditPageModel
        {
            Year = year,
            Code = 0,
            DocumentDate = DateOnly.FromDateTime(DateTime.Today),
            Causes = await ListCausesAsync(connection, cancellationToken),
            ContraAccounts = await ListPurchaseAccountsAsync(connection, cancellationToken),
            Stores = await ListStoresAsync(connection, cancellationToken),
            Payments = await ListPaymentsAsync(connection, cancellationToken),
            Banks = await ListBanksAsync(connection, cancellationToken)
        };
    }

    public async Task<PurchaseInvoiceSaveResult> SaveAsync(
        PurchaseInvoiceSaveCommand invoice,
        CancellationToken cancellationToken = default)
    {
        var vatRows = invoice.VatRows
            .Where(row => row.Taxable != 0 || row.Tax != 0)
            .Select(row => new PurchaseInvoiceVatSaveRow
            {
                Rate = decimal.Round(row.Rate, 2, MidpointRounding.AwayFromZero),
                Taxable = decimal.Round(row.Taxable, 2, MidpointRounding.AwayFromZero),
                Tax = decimal.Round(row.Tax, 2, MidpointRounding.AwayFromZero)
            })
            .ToArray();

        var taxableTotal = vatRows.Sum(row => row.Taxable);
        var vatTotal = vatRows.Sum(row => row.Tax);
        var insertedTotal = taxableTotal + vatTotal;
        var invoiceTotal = decimal.Round(invoice.Total, 2, MidpointRounding.AwayFromZero);
        var dueRows = invoice.DueRows
            .Where(row => row.Amount != 0 || row.Date is not null)
            .Select(row => new PurchaseInvoiceDueDateSaveRow
            {
                Number = row.Number,
                Amount = decimal.Round(row.Amount, 2, MidpointRounding.AwayFromZero),
                Date = row.Date,
                Paid = row.Paid
            })
            .ToArray();

        if (vatRows.Length == 0)
        {
            return new PurchaseInvoiceSaveResult(false, false, "Inserire almeno una riga imponibile/iva.");
        }

        if (decimal.Round(insertedTotal - invoiceTotal, 2, MidpointRounding.AwayFromZero) != 0)
        {
            return new PurchaseInvoiceSaveResult(false, false, "Quadratura iva errata.");
        }

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var existingInvoice = await ExistingInvoiceAsync(
            connection,
            invoice.DocumentNumber,
            invoice.DocumentDate!.Value,
            invoice.SupplierCode,
            cancellationToken);
        var editingInvoice = invoice.Id is > 0
            ? await ExistingInvoiceByIdAsync(connection, invoice.Id.Value, cancellationToken)
            : null;

        if (existingInvoice is not null
            && existingInvoice.Id != editingInvoice?.Id
            && !invoice.ConfirmOverwrite)
        {
            return new PurchaseInvoiceSaveResult(
                false,
                true,
                "Fattura gia' presente in archivio.",
                existingInvoice.Id,
                existingInvoice.Year,
                existingInvoice.Code);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var targetInvoice = editingInvoice ?? existingInvoice;
            var code = targetInvoice?.Code
                ?? await NextCodeAsync(connection, transaction, invoice.Year, cancellationToken);
            var id = targetInvoice?.Id
                ?? await InsertInvoiceAsync(
                    connection,
                    transaction,
                    invoice,
                    code,
                    taxableTotal,
                    vatTotal,
                     invoiceTotal,
                     cancellationToken);

            if (targetInvoice is not null)
            {
                await UpdateInvoiceAsync(
                    connection,
                    transaction,
                    invoice,
                    id,
                    code,
                    taxableTotal,
                    vatTotal,
                    invoiceTotal,
                    cancellationToken);
            }

            await DeleteVatRowsAsync(connection, transaction, id, cancellationToken);
            await InsertVatRowsAsync(
                connection,
                transaction,
                invoice,
                id,
                code,
                vatRows,
                cancellationToken);

            await ReplaceAccountingMovementAsync(
                connection,
                transaction,
                invoice,
                id,
                taxableTotal,
                vatTotal,
                invoiceTotal,
                cancellationToken);

            await ReplaceDueDatesAsync(
                connection,
                transaction,
                invoice,
                id,
                code,
                invoiceTotal,
                dueRows,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new PurchaseInvoiceSaveResult(
                true,
                false,
                "Fattura registrata.",
                id,
                invoice.Year,
                code);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static PurchaseInvoiceListItem? SelectInvoice(
        IReadOnlyList<PurchaseInvoiceListItem> invoices,
        int? selectedId) =>
        selectedId is null
            ? invoices.FirstOrDefault()
            : invoices.FirstOrDefault(invoice => invoice.Id == selectedId.Value)
              ?? invoices.FirstOrDefault();

    private sealed record ExistingPurchaseInvoice(int Id, int Year, int Code);

    private static async Task<ExistingPurchaseInvoice?> ExistingInvoiceAsync(
        MySqlConnection connection,
        string documentNumber,
        DateOnly documentDate,
        int supplierCode,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT ID, Anno, Codice
            FROM moviva
            WHERE Settore = @sector
              AND Ditta = @supplierCode
              AND DataDoc = @documentDate
              AND UPPER(TRIM(COALESCE(NumDoc, ''))) = UPPER(TRIM(@documentNumber))
            ORDER BY ID DESC
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);
        command.Parameters.AddWithValue("@supplierCode", supplierCode);
        command.Parameters.Add("@documentDate", MySqlDbType.Date).Value = documentDate.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@documentNumber", documentNumber.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ExistingPurchaseInvoice(
                Convert.ToInt32(reader["ID"]),
                Convert.ToInt32(reader["Anno"]),
                Convert.ToInt32(reader["Codice"]))
            : null;
    }

    private static async Task<ExistingPurchaseInvoice?> ExistingInvoiceByIdAsync(
        MySqlConnection connection,
        int id,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT ID, Anno, Codice
            FROM moviva
            WHERE ID = @id
              AND Settore = @sector
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ExistingPurchaseInvoice(
                Convert.ToInt32(reader["ID"]),
                Convert.ToInt32(reader["Anno"]),
                Convert.ToInt32(reader["Codice"]))
            : null;
    }

    private static async Task<IReadOnlyList<PurchaseInvoiceVatSaveRow>> InvoiceVatRowsAsync(
        MySqlConnection connection,
        int id,
        CancellationToken cancellationToken)
    {
        var rows = new List<PurchaseInvoiceVatSaveRow>();
        await using var command = new MySqlCommand(
            """
            SELECT COALESCE(AliqIva, 0) AS AliqIva,
                   COALESCE(Imponibile, 0) AS Imponibile,
                   COALESCE(Iva, 0) AS Iva
            FROM movivarg
            WHERE ID = @id
              AND Settore = @sector
            ORDER BY AliqIva, Imponibile, Iva;
            """,
            connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PurchaseInvoiceVatSaveRow
            {
                Rate = Convert.ToDecimal(reader["AliqIva"]),
                Taxable = Convert.ToDecimal(reader["Imponibile"]),
                Tax = Convert.ToDecimal(reader["Iva"])
            });
        }

        return rows;
    }

    private static async Task<IReadOnlyList<PurchaseInvoiceDueDateSaveRow>> InvoiceDueRowsAsync(
        MySqlConnection connection,
        int id,
        int year,
        int code,
        CancellationToken cancellationToken)
    {
        var hasInvoiceIdColumn = await ColumnExistsAsync(
            connection,
            null,
            "scadenze",
            "Fattura",
            cancellationToken);
        var hasPaidColumn = await ColumnExistsAsync(
            connection,
            null,
            "scadenze",
            "Pagata",
            cancellationToken);
        var paidSelect = hasPaidColumn ? "COALESCE(Pagata, 0)" : "0";
        var whereClause = hasInvoiceIdColumn
            ? "Fattura = @id"
            : "Anno = @year AND Settore = @sector AND Codice = @code";
        var rows = new List<PurchaseInvoiceDueDateSaveRow>();

        await using var command = new MySqlCommand(
            $"""
            SELECT COALESCE(Numero, 0) AS Numero,
                   COALESCE(Importo, 0) AS Importo,
                   DataScadenza,
                   {paidSelect} AS Pagata
            FROM scadenze
            WHERE {whereClause}
            ORDER BY Numero;
            """,
            connection);
        if (hasInvoiceIdColumn)
        {
            command.Parameters.AddWithValue("@id", id);
        }
        else
        {
            command.Parameters.AddWithValue("@year", year);
            command.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);
            command.Parameters.AddWithValue("@code", code);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PurchaseInvoiceDueDateSaveRow
            {
                Number = Convert.ToInt32(reader["Numero"]),
                Amount = Convert.ToDecimal(reader["Importo"]),
                Date = reader["DataScadenza"] is DateTime dueDate
                    ? DateOnly.FromDateTime(dueDate)
                    : null,
                Paid = Convert.ToInt32(reader["Pagata"]) != 0
            });
        }

        return rows;
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
            FROM moviva
            WHERE Anno = @year
              AND Settore = @sector;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> InsertInvoiceAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        PurchaseInvoiceSaveCommand invoice,
        int code,
        decimal taxableTotal,
        decimal vatTotal,
        decimal invoiceTotal,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            INSERT INTO moviva
                (Anno, Settore, Codice, Causale, NumDoc, DataDoc, Ditta, CtPartita, PuntoV,
                 Pagamento, Banca, FeName, Notes)
            VALUES
                (@year, @sector, @code, @cause, @documentNumber, @documentDate, @supplierCode, @contraAccountCode,
                 @storeCode, @paymentCode, @bankCode, @fileName, @notes);
            """,
            connection,
            transaction);
        AddInvoiceParameters(command, invoice, code, taxableTotal, vatTotal, invoiceTotal);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return Convert.ToInt32(command.LastInsertedId);
    }

    private static async Task UpdateInvoiceAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        PurchaseInvoiceSaveCommand invoice,
        int id,
        int code,
        decimal taxableTotal,
        decimal vatTotal,
        decimal invoiceTotal,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            UPDATE moviva
            SET Anno = @year,
                Settore = @sector,
                Codice = @code,
                Causale = @cause,
                NumDoc = @documentNumber,
                DataDoc = @documentDate,
                Ditta = @supplierCode,
                CtPartita = @contraAccountCode,
                PuntoV = @storeCode,
                Pagamento = @paymentCode,
                Banca = @bankCode,
                FeName = @fileName,
                Notes = @notes
            WHERE ID = @id;
            """,
            connection,
            transaction);
        AddInvoiceParameters(command, invoice, code, taxableTotal, vatTotal, invoiceTotal);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddInvoiceParameters(
        MySqlCommand command,
        PurchaseInvoiceSaveCommand invoice,
        int code,
        decimal taxableTotal,
        decimal vatTotal,
        decimal invoiceTotal)
    {
        command.Parameters.AddWithValue("@year", invoice.Year);
        command.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@cause", invoice.CauseCode);
        command.Parameters.AddWithValue("@documentNumber", invoice.DocumentNumber.Trim());
        command.Parameters.Add("@documentDate", MySqlDbType.Date).Value = invoice.DocumentDate!.Value.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@supplierCode", invoice.SupplierCode);
        command.Parameters.AddWithValue("@contraAccountCode", invoice.ContraAccountCode);
        command.Parameters.AddWithValue("@storeCode", invoice.StoreCode);
        command.Parameters.AddWithValue("@taxableTotal", taxableTotal);
        command.Parameters.AddWithValue("@vatTotal", vatTotal);
        command.Parameters.AddWithValue("@invoiceTotal", invoiceTotal);
        command.Parameters.AddWithValue("@paymentCode", invoice.PaymentCode);
        command.Parameters.AddWithValue("@bankCode", invoice.BankCode);
        command.Parameters.AddWithValue("@fileName", invoice.ElectronicInvoiceFileName.Trim());
        command.Parameters.AddWithValue("@notes", invoice.Notes.Trim());
    }

    private static async Task DeleteVatRowsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int id,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "DELETE FROM movivarg WHERE ID = @id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertVatRowsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        PurchaseInvoiceSaveCommand invoice,
        int id,
        int code,
        IReadOnlyList<PurchaseInvoiceVatSaveRow> vatRows,
        CancellationToken cancellationToken)
    {
        foreach (var row in vatRows)
        {
            await using var command = new MySqlCommand(
                """
                INSERT INTO movivarg
                    (ID, Anno, Codice, Settore, PuntoV, AliqIva, Imponibile, Iva)
                VALUES
                    (@id, @year, @code, @sector, @storeCode, @vatRate, @taxable, @tax);
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@year", invoice.Year);
            command.Parameters.AddWithValue("@code", code);
            command.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);
            command.Parameters.AddWithValue("@storeCode", invoice.StoreCode);
            command.Parameters.AddWithValue("@vatRate", row.Rate);
            command.Parameters.AddWithValue("@taxable", row.Taxable);
            command.Parameters.AddWithValue("@tax", row.Tax);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private sealed record ExistingAccountingMovement(int Id, int Code);

    private static async Task<ExistingAccountingMovement?> ExistingAccountingMovementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int invoiceId,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT ID, Codice
            FROM movcont
            WHERE Settore = @sector
              AND Documento = @document
            LIMIT 1;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);
        command.Parameters.AddWithValue("@document", invoiceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ExistingAccountingMovement(
                Convert.ToInt32(reader["ID"]),
                Convert.ToInt32(reader["Codice"]))
            : null;
    }

    private static async Task<int> NextAccountingCodeAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COALESCE(MAX(Codice), 0) + 1 FROM movcont WHERE Anno = @year;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@year", year);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task ReplaceAccountingMovementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        PurchaseInvoiceSaveCommand invoice,
        int invoiceId,
        decimal taxableTotal,
        decimal vatTotal,
        decimal invoiceTotal,
        CancellationToken cancellationToken)
    {
        var existingMovement = await ExistingAccountingMovementAsync(
            connection,
            transaction,
            invoiceId,
            cancellationToken);
        var accountingCode = existingMovement?.Code
            ?? await NextAccountingCodeAsync(connection, transaction, invoice.Year, cancellationToken);
        var movementId = existingMovement?.Id
            ?? await InsertAccountingMovementAsync(
                connection,
                transaction,
                invoice,
                invoiceId,
                accountingCode,
                invoiceTotal,
                cancellationToken);

        if (existingMovement is not null)
        {
            await UpdateAccountingMovementAsync(
                connection,
                transaction,
                invoice,
                invoiceId,
                movementId,
                accountingCode,
                invoiceTotal,
                cancellationToken);
        }

        await DeleteAccountingRowsAsync(connection, transaction, movementId, cancellationToken);
        await InsertPurchaseAccountingRowsAsync(
            connection,
            transaction,
            invoice,
            movementId,
            accountingCode,
            taxableTotal,
            vatTotal,
            invoiceTotal,
            cancellationToken);
    }

    private static async Task<int> InsertAccountingMovementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        PurchaseInvoiceSaveCommand invoice,
        int invoiceId,
        int accountingCode,
        decimal invoiceTotal,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            INSERT INTO movcont
                (Anno, Settore, Codice, Causale, DataMov, CliFor, Ditta, NumDoc,
                 Documento, Importo, PuntoV, Note)
            VALUES
                (@year, @sector, @code, @cause, @movementDate, @subjectType, @supplierCode, @documentNumber,
                 @document, @amount, @storeCode, @notes);
            """,
            connection,
            transaction);
        AddAccountingMovementParameters(command, invoice, invoiceId, accountingCode, invoiceTotal);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return Convert.ToInt32(command.LastInsertedId);
    }

    private static async Task UpdateAccountingMovementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        PurchaseInvoiceSaveCommand invoice,
        int invoiceId,
        int movementId,
        int accountingCode,
        decimal invoiceTotal,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            UPDATE movcont
            SET Anno = @year,
                Settore = @sector,
                Codice = @code,
                Causale = @cause,
                DataMov = @movementDate,
                CliFor = @subjectType,
                Ditta = @supplierCode,
                NumDoc = @documentNumber,
                Documento = @document,
                Importo = @amount,
                PuntoV = @storeCode,
                Note = @notes
            WHERE ID = @id;
            """,
            connection,
            transaction);
        AddAccountingMovementParameters(command, invoice, invoiceId, accountingCode, invoiceTotal);
        command.Parameters.AddWithValue("@id", movementId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddAccountingMovementParameters(
        MySqlCommand command,
        PurchaseInvoiceSaveCommand invoice,
        int invoiceId,
        int accountingCode,
        decimal invoiceTotal)
    {
        command.Parameters.AddWithValue("@year", invoice.Year);
        command.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);
        command.Parameters.AddWithValue("@code", accountingCode);
        command.Parameters.AddWithValue("@cause", invoice.CauseCode);
        command.Parameters.Add("@movementDate", MySqlDbType.Date).Value = invoice.DocumentDate!.Value.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@subjectType", SupplierSubjectType);
        command.Parameters.AddWithValue("@supplierCode", invoice.SupplierCode);
        command.Parameters.AddWithValue("@documentNumber", invoice.DocumentNumber.Trim());
        command.Parameters.AddWithValue("@document", invoiceId);
        command.Parameters.AddWithValue("@amount", invoiceTotal);
        command.Parameters.AddWithValue("@storeCode", invoice.StoreCode);
        command.Parameters.AddWithValue("@notes", invoice.Notes.Trim());
    }

    private static async Task DeleteAccountingRowsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int movementId,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "DELETE FROM movcontrg WHERE ID = @id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@id", movementId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertPurchaseAccountingRowsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        PurchaseInvoiceSaveCommand invoice,
        int movementId,
        int accountingCode,
        decimal taxableTotal,
        decimal vatTotal,
        decimal invoiceTotal,
        CancellationToken cancellationToken)
    {
        var debitSign = invoice.CauseCode == 12 ? "A" : "D";
        var creditSign = invoice.CauseCode == 12 ? "D" : "A";
        var rowNumber = 1;

        rowNumber += await InsertAccountingRowIfNotZeroAsync(
            connection,
            transaction,
            invoice,
            movementId,
            accountingCode,
            rowNumber,
            invoice.ContraAccountCode,
            taxableTotal,
            debitSign,
            cancellationToken);

        rowNumber += await InsertAccountingRowIfNotZeroAsync(
            connection,
            transaction,
            invoice,
            movementId,
            accountingCode,
            rowNumber,
            PurchaseVatAccountCode,
            vatTotal,
            debitSign,
            cancellationToken);

        await InsertAccountingRowIfNotZeroAsync(
            connection,
            transaction,
            invoice,
            movementId,
            accountingCode,
            rowNumber,
            SupplierAccountCode,
            invoiceTotal,
            creditSign,
            cancellationToken);
    }

    private static async Task<int> InsertAccountingRowIfNotZeroAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        PurchaseInvoiceSaveCommand invoice,
        int movementId,
        int accountingCode,
        int rowNumber,
        int accountCode,
        decimal amount,
        string sign,
        CancellationToken cancellationToken)
    {
        if (amount == 0)
        {
            return 0;
        }

        await using var command = new MySqlCommand(
            """
            INSERT INTO movcontrg
                (ID, Anno, Settore, Codice, Riga, Conto, Importo, Segno)
            VALUES
                (@id, @year, @sector, @code, @rowNumber, @accountCode, @amount, @sign);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@id", movementId);
        command.Parameters.AddWithValue("@year", invoice.Year);
        command.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);
        command.Parameters.AddWithValue("@code", accountingCode);
        command.Parameters.AddWithValue("@rowNumber", rowNumber);
        command.Parameters.AddWithValue("@accountCode", accountCode);
        command.Parameters.AddWithValue("@amount", amount);
        command.Parameters.AddWithValue("@sign", sign);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return 1;
    }

    private sealed record PaymentDueDateInfo(int TitleType);

    private static async Task ReplaceDueDatesAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        PurchaseInvoiceSaveCommand invoice,
        int invoiceId,
        int code,
        decimal invoiceTotal,
        IReadOnlyList<PurchaseInvoiceDueDateSaveRow> dueRows,
        CancellationToken cancellationToken)
    {
        await DeleteDueDatesAsync(connection, transaction, invoice.Year, code, cancellationToken);
        if (invoice.PaymentCode <= 0 || dueRows.Count == 0)
        {
            return;
        }

        var paymentInfo = await PaymentDueDateInfoAsync(
            connection,
            transaction,
            invoice.PaymentCode,
            cancellationToken);
        var hasPaidColumn = await ColumnExistsAsync(
            connection,
            transaction,
            "scadenze",
            "Pagata",
            cancellationToken);
        var hasInvoiceIdColumn = await ColumnExistsAsync(
            connection,
            transaction,
            "scadenze",
            "Fattura",
            cancellationToken);
        var hasInvoiceNumberColumn = await ColumnExistsAsync(
            connection,
            transaction,
            "scadenze",
            "NumeroFatt",
            cancellationToken);

        var rowNumber = 1;
        foreach (var dueRow in dueRows.Where(row => row.Amount != 0))
        {
            if (dueRow.Date is null)
            {
                continue;
            }

            await InsertDueDateAsync(
                connection,
                transaction,
                invoice,
                invoiceId,
                code,
                rowNumber++,
                invoiceTotal,
                paymentInfo.TitleType,
                dueRow,
                hasInvoiceIdColumn,
                hasInvoiceNumberColumn,
                hasPaidColumn,
                cancellationToken);
        }
    }

    private static async Task DeleteDueDatesAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int code,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            DELETE FROM scadenze
            WHERE Anno = @year
              AND Settore = @sector
              AND Codice = @code;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);
        command.Parameters.AddWithValue("@code", code);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<PaymentDueDateInfo> PaymentDueDateInfoAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int paymentCode,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT COALESCE(TipoTitolo, 0) AS TipoTitolo
            FROM pagamenti
            WHERE Codice = @code
            LIMIT 1;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@code", paymentCode);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return new PaymentDueDateInfo(value is null || value == DBNull.Value ? 0 : Convert.ToInt32(value));
    }

    private static async Task InsertDueDateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        PurchaseInvoiceSaveCommand invoice,
        int invoiceId,
        int code,
        int rowNumber,
        decimal invoiceTotal,
        int titleType,
        PurchaseInvoiceDueDateSaveRow dueRow,
        bool hasInvoiceIdColumn,
        bool hasInvoiceNumberColumn,
        bool hasPaidColumn,
        CancellationToken cancellationToken)
    {
        var invoiceIdField = hasInvoiceIdColumn ? ", Fattura" : "";
        var invoiceIdValue = hasInvoiceIdColumn ? ", @invoiceId" : "";
        var invoiceNumberField = hasInvoiceNumberColumn ? ", NumeroFatt" : "";
        var invoiceNumberValue = hasInvoiceNumberColumn ? ", @documentNumber" : "";
        var paidField = hasPaidColumn ? ", Pagata" : "";
        var paidValue = hasPaidColumn ? ", @paid" : "";
        await using var command = new MySqlCommand(
            $"""
            INSERT INTO scadenze
                (Anno, Settore, Codice, Numero, CodPagamento,
                 TipoTitolo, DataScadenza, CliFor, Ditta,
                 DataFatt, TotaleFatt, Banca, Importo{invoiceIdField}{invoiceNumberField}{paidField})
            VALUES
                (@year, @sector, @code, @number, @paymentCode,
                 @titleType, @dueDate, @subjectType, @supplierCode,
                 @documentDate, @invoiceTotal, @bankCode, @amount{invoiceIdValue}{invoiceNumberValue}{paidValue});
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@year", invoice.Year);
        command.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@number", rowNumber);
        command.Parameters.AddWithValue("@paymentCode", invoice.PaymentCode);
        command.Parameters.AddWithValue("@titleType", titleType);
        command.Parameters.Add("@dueDate", MySqlDbType.Date).Value = dueRow.Date!.Value.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@subjectType", SupplierSubjectType);
        command.Parameters.AddWithValue("@supplierCode", invoice.SupplierCode);
        command.Parameters.Add("@documentDate", MySqlDbType.Date).Value = invoice.DocumentDate!.Value.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@invoiceTotal", invoiceTotal);
        command.Parameters.AddWithValue("@bankCode", invoice.BankCode);
        command.Parameters.AddWithValue("@amount", dueRow.Amount);
        if (hasInvoiceIdColumn)
        {
            command.Parameters.AddWithValue("@invoiceId", invoiceId);
        }

        if (hasInvoiceNumberColumn)
        {
            command.Parameters.AddWithValue("@documentNumber", invoice.DocumentNumber.Trim());
        }

        if (hasPaidColumn)
        {
            command.Parameters.AddWithValue("@paid", dueRow.Paid ? 1 : 0);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> ColumnExistsAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND LOWER(TABLE_NAME) = LOWER(@tableName)
              AND LOWER(COLUMN_NAME) = LOWER(@columnName);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@tableName", tableName);
        command.Parameters.AddWithValue("@columnName", columnName);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<IReadOnlyList<PurchaseInvoiceListItem>> ListInvoicesAsync(
        MySqlConnection connection,
        int year,
        int? month,
        int? causeCode,
        int? supplierCode,
        int? storeCode,
        int? contraAccountCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT mv.ID,
                   mv.Anno,
                   mv.Codice,
                   COALESCE(mv.NumDoc, '') AS NumDoc,
                   mv.DataDoc,
                   COALESCE(mv.Causale, 0) AS Causale,
                   COALESCE(cc.Descrizione, '') AS CausaleDescrizione,
                   COALESCE(mv.Ditta, 0) AS Ditta,
                   COALESCE(f.Nome, '') AS Fornitore,
                   COALESCE(mv.CtPartita, 0) AS CtPartita,
                   COALESCE(cn.Descrizione, '') AS Contropartita,
                   COALESCE(vat.Imponibile, 0) AS Imponibile,
                   COALESCE(vat.Iva, 0) AS Iva,
                   COALESCE(vat.Imponibile, 0) + COALESCE(vat.Iva, 0) AS Totale,
                   COALESCE(mv.FeName, '') AS FeName
            FROM moviva mv
            LEFT JOIN (
                SELECT ID,
                       SUM(COALESCE(Imponibile, 0)) AS Imponibile,
                       SUM(COALESCE(Iva, 0)) AS Iva
                FROM movivarg
                WHERE Settore = @sector
                GROUP BY ID
            ) vat ON vat.ID = mv.ID
            LEFT JOIN causalicont cc ON cc.Codice = mv.Causale
            LEFT JOIN fornitori f ON f.Codice = mv.Ditta
            LEFT JOIN conti cn ON cn.Codice = mv.CtPartita
            WHERE mv.Settore = @sector
              AND mv.Anno = @year
              AND (@month IS NULL OR MONTH(mv.DataDoc) = @month)
              AND (@causeCode IS NULL OR mv.Causale = @causeCode)
              AND (@supplierCode IS NULL OR mv.Ditta = @supplierCode)
              AND (
                  @storeCode IS NULL
                  OR (@storeCode = 0 AND COALESCE(mv.PuntoV, 0) = 0)
                  OR mv.PuntoV = @storeCode
              )
              AND (@contraAccountCode IS NULL OR mv.CtPartita = @contraAccountCode)
            ORDER BY mv.Anno DESC, mv.DataDoc DESC, mv.Codice DESC;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@month", month is null ? DBNull.Value : month.Value);
        command.Parameters.AddWithValue("@causeCode", causeCode is null ? DBNull.Value : causeCode.Value);
        command.Parameters.AddWithValue("@supplierCode", supplierCode is null ? DBNull.Value : supplierCode.Value);
        command.Parameters.AddWithValue("@storeCode", storeCode is null ? DBNull.Value : storeCode.Value);
        command.Parameters.AddWithValue("@contraAccountCode", contraAccountCode is null ? DBNull.Value : contraAccountCode.Value);

        var rows = new List<PurchaseInvoiceListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PurchaseInvoiceListItem(
                Convert.ToInt32(reader["ID"]),
                Convert.ToInt32(reader["Anno"]),
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["NumDoc"]) ?? "",
                DateOnly.FromDateTime(Convert.ToDateTime(reader["DataDoc"])),
                Convert.ToInt32(reader["Causale"]),
                Convert.ToString(reader["CausaleDescrizione"]) ?? "",
                Convert.ToInt32(reader["Ditta"]),
                Convert.ToString(reader["Fornitore"]) ?? "",
                Convert.ToInt32(reader["CtPartita"]),
                Convert.ToString(reader["Contropartita"]) ?? "",
                Money(reader["Imponibile"]),
                Money(reader["Iva"]),
                Money(reader["Totale"]),
                Convert.ToString(reader["FeName"]) ?? ""));
        }

        return rows;
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

    private static async Task<IReadOnlyList<int>> ListYearsAsync(
        MySqlConnection connection,
        int currentYear,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT Anno
            FROM moviva
            WHERE Settore = @sector
            ORDER BY Anno DESC;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@sector", PurchaseInvoiceSector);

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

    private static async Task<IReadOnlyList<PurchaseInvoiceCauseOption>> ListCausesAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione
            FROM causalicont
            WHERE Codice IN (10, 11, 12)
            ORDER BY Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<PurchaseInvoiceCauseOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PurchaseInvoiceCauseOption(
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Descrizione"]) ?? ""));
        }

        return rows;
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
        var rows = new List<PurchaseInvoiceStoreOption>();
        rows.Add(new PurchaseInvoiceStoreOption(0, "Spese comuni"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = Convert.ToInt32(reader["Codice"]);
            var name = Convert.ToString(reader["Nome"]) ?? "";
            rows.Add(new PurchaseInvoiceStoreOption(code, $"{code:000} - {name}"));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<PurchaseInvoiceAccountOption>> ListContraAccountsAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT mv.CtPartita AS Codice,
                   COALESCE(c.Descrizione, '') AS Descrizione
            FROM moviva mv
            LEFT JOIN conti c ON c.Codice = mv.CtPartita
            WHERE mv.CtPartita > 0
            ORDER BY Descrizione, Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<PurchaseInvoiceAccountOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = Convert.ToInt32(reader["Codice"]);
            var description = Convert.ToString(reader["Descrizione"]) ?? "";
            rows.Add(new PurchaseInvoiceAccountOption(code, $"{code:000} - {description}"));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<PurchaseInvoiceAccountOption>> ListPurchaseAccountsAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione
            FROM conti
            WHERE (Tipo = 'C' OR Mastro = 1)
              AND COALESCE(Ditta, '') = 'F'
            ORDER BY Descrizione, Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<PurchaseInvoiceAccountOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = Convert.ToInt32(reader["Codice"]);
            var description = Convert.ToString(reader["Descrizione"]) ?? "";
            rows.Add(new PurchaseInvoiceAccountOption(code, $"{code:000} - {description}"));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<PurchaseInvoicePaymentOption>> ListPaymentsAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione
            FROM pagamenti
            ORDER BY Descrizione, Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<PurchaseInvoicePaymentOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = Convert.ToInt32(reader["Codice"]);
            var description = Convert.ToString(reader["Descrizione"]) ?? "";
            rows.Add(new PurchaseInvoicePaymentOption(code, $"{code:000} - {description}"));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<PurchaseInvoiceBankOption>> ListBanksAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Codice, COALESCE(Descrizione, '') AS Nome
            FROM conti
            WHERE COALESCE(Ditta, '') = 'B'
            ORDER BY Descrizione, Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<PurchaseInvoiceBankOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = Convert.ToInt32(reader["Codice"]);
            var name = Convert.ToString(reader["Nome"]) ?? "";
            rows.Add(new PurchaseInvoiceBankOption(code, $"{code:000} - {name}"));
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

    private static PurchaseInvoiceTotals TotalsFrom(IReadOnlyList<PurchaseInvoiceListItem> rows) =>
        new(rows.Sum(row => row.Net), rows.Sum(row => row.Vat), rows.Sum(row => row.Total));

    private static decimal Money(object value) =>
        value is null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
}

