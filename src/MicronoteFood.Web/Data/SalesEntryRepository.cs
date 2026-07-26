using System.Globalization;
using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class SalesEntryRepository(MicronoteDb database)
{
    private const int SalesVatSector = 20;
    private const int SalesCause = 20;
    private const decimal DefaultSalesVatRate = 10m;

    public async Task<SalesEntryPageModel> GetNewAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        return new SalesEntryPageModel
        {
            Year = year,
            Code = await NextCodeAsync(connection, year, cancellationToken),
            MovementDate = DateOnly.FromDateTime(DateTime.Today),
            LastRegistrationDate = await LastRegistrationDateAsync(connection, year, cancellationToken),
            SalesVatRate = await SalesVatRateAsync(connection, cancellationToken),
            Stores = await ListStoresAsync(connection, cancellationToken)
        };
    }

    public async Task<SalesEntryPageModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        const string saleSql = """
            SELECT ID, Anno, Codice, DataMov
            FROM Vendite
            WHERE ID = @id
            LIMIT 1;
            """;

        int year;
        int code;
        DateOnly movementDate;
        await using (var command = new MySqlCommand(saleSql, connection))
        {
            command.Parameters.AddWithValue("@id", id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            year = Convert.ToInt32(reader["Anno"]);
            code = Convert.ToInt32(reader["Codice"]);
            movementDate = DateOnly.FromDateTime(Convert.ToDateTime(reader["DataMov"]));
        }

        var rows = await ListSavedRowsForSaleAsync(connection, id, cancellationToken);

        return new SalesEntryPageModel
        {
            SaleId = id,
            Year = year,
            Code = code,
            MovementDate = movementDate,
            LastRegistrationDate = await LastRegistrationDateAsync(connection, year, cancellationToken),
            SalesVatRate = await SalesVatRateAsync(connection, cancellationToken),
            Stores = rows
                .Select(row => new SalesEntryStoreRow(row.StoreCode, row.StoreName))
                .ToList(),
            SavedRows = rows
        };
    }

    public async Task<SalesEntrySaveResult> SaveSalesOnlyAsync(
        SalesEntrySaveCommand sale,
        bool confirmOverwrite,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        var existingSale = sale.SaleId.HasValue
            ? await ExistingSaleByIdAsync(connection, sale.SaleId.Value, cancellationToken)
            : null;
        var excludedId = existingSale?.Id;
        var existingCode = await ExistingCodeByDateAsync(
            connection,
            sale.Year,
            sale.MovementDate,
            excludedId,
            cancellationToken);

        if (existingCode is not null && !confirmOverwrite)
        {
            return new SalesEntrySaveResult(
                false,
                existingCode.Value,
                true,
                sale.MovementDate);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var code = existingSale?.Code
                ?? existingCode
                ?? await NextCodeAsync(connection, transaction, sale.Year, cancellationToken);
            var totals = SalesEntryTotals.FromRows(sale.Rows);
            var replacedSale = existingSale
                ?? (existingCode is null
                    ? null
                    : await ExistingSaleByYearCodeAsync(
                        connection,
                        transaction,
                        sale.Year,
                        existingCode.Value,
                        cancellationToken));

            var accountingMovement = replacedSale is null
                ? null
                : await ExistingAccountingMovementAsync(
                    connection,
                    transaction,
                    replacedSale.Id,
                    cancellationToken);
            var accountingCode = accountingMovement?.Code
                ?? await NextAccountingCodeAsync(connection, transaction, sale.Year, cancellationToken);

            if (accountingMovement is not null)
            {
                await DeleteAccountingMovementAsync(
                    connection,
                    transaction,
                    accountingMovement.Id,
                    cancellationToken);
            }

            await DeleteVatRowsAsync(connection, transaction, sale.Year, code, cancellationToken);
            await DeleteVatMovementAsync(connection, transaction, sale.Year, code, cancellationToken);

            int saleId;
            if (existingSale is not null)
            {
                saleId = existingSale.Id;
                await DeleteSalesRowsByIdAsync(connection, transaction, saleId, cancellationToken);
                await UpdateSaleAsync(
                    connection,
                    transaction,
                    saleId,
                    sale.Year,
                    code,
                    sale.MovementDate,
                    totals,
                    cancellationToken);
            }
            else
            {
                await DeleteSalesRowsAsync(connection, transaction, sale.Year, code, cancellationToken);
                await DeleteSaleAsync(connection, transaction, sale.Year, code, cancellationToken);
                saleId = await InsertSaleAsync(
                    connection,
                    transaction,
                    sale.Year,
                    code,
                    sale.MovementDate,
                    totals,
                    cancellationToken);
            }

            foreach (var row in sale.Rows)
            {
                await InsertSaleRowAsync(
                    connection,
                    transaction,
                    saleId,
                    sale.Year,
                    code,
                    row,
                    cancellationToken);
            }

            var vatMovementId = await InsertVatMovementAsync(
                connection,
                transaction,
                sale.Year,
                code,
                sale.MovementDate,
                cancellationToken);

            foreach (var row in sale.Rows)
            {
                await InsertVatRowsAsync(
                    connection,
                    transaction,
                    vatMovementId,
                    sale.Year,
                    code,
                    sale.SalesVatRate,
                    row,
                    cancellationToken);
            }

            await InsertAccountingMovementAsync(
                connection,
                transaction,
                sale.Year,
                accountingCode,
                saleId,
                sale.MovementDate,
                totals,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new SalesEntrySaveResult(true, code, false, null);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> DeleteByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var sale = await ExistingSaleByIdAsync(connection, id, cancellationToken);
        if (sale is null)
        {
            return false;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var accountingMovement = await ExistingAccountingMovementAsync(
                connection,
                transaction,
                sale.Id,
                cancellationToken);

            if (accountingMovement is not null)
            {
                await DeleteAccountingMovementAsync(
                    connection,
                    transaction,
                    accountingMovement.Id,
                    cancellationToken);
            }

            await DeleteVatRowsAsync(connection, transaction, sale.Year, sale.Code, cancellationToken);
            await DeleteVatMovementAsync(connection, transaction, sale.Year, sale.Code, cancellationToken);
            await DeleteSalesRowsByIdAsync(connection, transaction, id, cancellationToken);
            await DeleteSaleByIdAsync(connection, transaction, id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<int?> ExistingCodeByDateAsync(
        MySqlConnection connection,
        int year,
        DateOnly movementDate,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT Codice
            FROM Vendite
            WHERE Anno = @year
              AND DataMov = @movementDate
              AND (@excludedId IS NULL OR ID <> @excludedId)
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@movementDate", movementDate.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@excludedId", excludedId);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value == DBNull.Value ? null : Convert.ToInt32(value);
    }

    private static async Task<SaleKey?> ExistingSaleByIdAsync(
        MySqlConnection connection,
        int id,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT ID, Anno, Codice
            FROM Vendite
            WHERE ID = @id
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SaleKey(
            Convert.ToInt32(reader["ID"]),
            Convert.ToInt32(reader["Anno"]),
            Convert.ToInt32(reader["Codice"]));
    }

    private static async Task<SaleKey?> ExistingSaleByYearCodeAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int code,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT ID, Anno, Codice
            FROM Vendite
            WHERE Anno = @year
              AND Codice = @code
            LIMIT 1;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SaleKey(
            Convert.ToInt32(reader["ID"]),
            Convert.ToInt32(reader["Anno"]),
            Convert.ToInt32(reader["Codice"]));
    }

    private static async Task<decimal> SalesVatRateAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultSalesVatRateAsync(connection, cancellationToken);

        var optionRate = await OptionValueAsync(connection, cancellationToken);
        return optionRate is > 0 ? optionRate.Value : DefaultSalesVatRate;
    }

    private static async Task EnsureDefaultSalesVatRateAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = new MySqlCommand(
                """
                INSERT INTO Opzioni (Chiave, Valore)
                SELECT 'AliqIvaVendite', @defaultValue
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM Opzioni
                    WHERE Chiave = 'AliqIvaVendite'
                );

                UPDATE Opzioni
                SET Valore = @defaultValue
                WHERE Chiave = 'AliqIvaVendite'
                  AND (Valore IS NULL OR TRIM(Valore) = '');
                """,
                connection);
            command.Parameters.AddWithValue("@defaultValue", DefaultSalesVatRate.ToString(CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException ex) when (ex.Number is 1054 or 1146)
        {
            // Older or incomplete databases still get the in-code default.
        }
    }

    private static async Task<decimal?> OptionValueAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = new MySqlCommand(
                """
                SELECT Valore
                FROM Opzioni
                WHERE Chiave IN ('AliqIvaVendite', 'AliquotaIvaVendite')
                LIMIT 1;
                """,
                connection);

            var value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is null || value == DBNull.Value)
            {
                return null;
            }

            return ParseRate(Convert.ToString(value));
        }
        catch (MySqlException ex) when (ex.Number is 1054 or 1146)
        {
            return null;
        }
    }

    private static decimal? ParseRate(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        normalized = normalized.Replace(".", "").Replace(",", ".");
        return decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var rate)
            && rate > 0
            ? rate
            : null;
    }

    private static async Task<int> NextCodeAsync(
        MySqlConnection connection,
        int year,
        CancellationToken cancellationToken)
    {
        return await NextCodeAsync(connection, null, year, cancellationToken);
    }

    private static async Task<int> NextCodeAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        int year,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COALESCE(MAX(Codice), 0) + 1 FROM Vendite WHERE Anno = @year;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@year", year);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
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

    private static async Task<AccountingMovementKey?> ExistingAccountingMovementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int saleId,
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
        command.Parameters.AddWithValue("@sector", SalesVatSector);
        command.Parameters.AddWithValue("@document", saleId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AccountingMovementKey(
            Convert.ToInt32(reader["ID"]),
            Convert.ToInt32(reader["Codice"]));
    }

    private static async Task DeleteSaleAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int code,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "DELETE FROM Vendite WHERE Anno = @year AND Codice = @code;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@code", code);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteSaleByIdAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int id,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "DELETE FROM Vendite WHERE ID = @id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteSalesRowsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int code,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "DELETE FROM VenditeRg WHERE Anno = @year AND Codice = @code;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@code", code);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteSalesRowsByIdAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int id,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "DELETE FROM VenditeRg WHERE ID = @id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteVatMovementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int code,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "DELETE FROM moviva WHERE Anno = @year AND Settore = @sector AND Codice = @code;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@sector", SalesVatSector);
        command.Parameters.AddWithValue("@code", code);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteVatRowsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int code,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "DELETE FROM movivarg WHERE Anno = @year AND Settore = @sector AND Codice = @code;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@sector", SalesVatSector);
        command.Parameters.AddWithValue("@code", code);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteAccountingMovementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int id,
        CancellationToken cancellationToken)
    {
        await using (var rowsCommand = new MySqlCommand(
            "DELETE FROM movcontrg WHERE ID = @id;",
            connection,
            transaction))
        {
            rowsCommand.Parameters.AddWithValue("@id", id);
            await rowsCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = new MySqlCommand(
            "DELETE FROM movcont WHERE ID = @id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> InsertSaleAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int code,
        DateOnly movementDate,
        SalesEntryTotals totals,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            INSERT INTO Vendite
                (Anno, Codice, DataMov,
                 Contanti, Carta, Tickets, Assegni, Altro, Sospesi, Perdite)
            VALUES
                (@year, @code, @movementDate,
                 @cash, @card, @tickets, @checks, @other, @suspended, @losses);
            """,
            connection,
            transaction);

        AddSaleParameters(command, year, code, movementDate, totals);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return Convert.ToInt32(command.LastInsertedId);
    }

    private static async Task UpdateSaleAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int id,
        int year,
        int code,
        DateOnly movementDate,
        SalesEntryTotals totals,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            UPDATE Vendite
            SET Anno = @year,
                Codice = @code,
                DataMov = @movementDate,
                Contanti = @cash,
                Carta = @card,
                Tickets = @tickets,
                Assegni = @checks,
                Altro = @other,
                Sospesi = @suspended,
                Perdite = @losses
            WHERE ID = @id;
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("@id", id);
        AddSaleParameters(command, year, code, movementDate, totals);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertSaleRowAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int id,
        int year,
        int code,
        SalesEntrySaveCommandRow row,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            INSERT INTO VenditeRg
                (ID, Anno, Codice, PuntoV,
                 Contanti, Carta, Tickets, Assegni, Altro, Sospesi, Perdite)
            VALUES
                (@id, @year, @code, @storeCode,
                 @cash, @card, @tickets, @checks, @other, @suspended, @losses);
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@storeCode", row.StoreCode);
        command.Parameters.AddWithValue("@cash", row.Cash);
        command.Parameters.AddWithValue("@card", row.Card);
        command.Parameters.AddWithValue("@tickets", row.Tickets);
        command.Parameters.AddWithValue("@checks", row.Checks);
        command.Parameters.AddWithValue("@other", row.Other);
        command.Parameters.AddWithValue("@suspended", row.Suspended);
        command.Parameters.AddWithValue("@losses", row.Losses);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> InsertVatMovementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int code,
        DateOnly movementDate,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            INSERT INTO moviva
                (Anno, Settore, Codice, Causale, DataDoc)
            VALUES
                (@year, @sector, @code, @cause, @documentDate);
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@sector", SalesVatSector);
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@cause", SalesCause);
        command.Parameters.AddWithValue("@documentDate", movementDate.ToDateTime(TimeOnly.MinValue));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return Convert.ToInt32(command.LastInsertedId);
    }

    private static async Task InsertVatRowsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int id,
        int year,
        int code,
        decimal vatRate,
        SalesEntrySaveCommandRow row,
        CancellationToken cancellationToken)
    {
        var taxableNet = row.Net - row.Exempt;
        if (taxableNet > 0 || row.Vat > 0)
        {
            await InsertVatRowAsync(
                connection,
                transaction,
                id,
                year,
                code,
                row.StoreCode,
                vatRate,
                taxableNet,
                row.Vat,
                cancellationToken);
        }

        if (row.Exempt > 0)
        {
            await InsertVatRowAsync(
                connection,
                transaction,
                id,
                year,
                code,
                row.StoreCode,
                0,
                row.Exempt,
                0,
                cancellationToken);
        }
    }

    private static async Task InsertVatRowAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int id,
        int year,
        int code,
        int storeCode,
        decimal vatRate,
        decimal net,
        decimal vat,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            INSERT INTO movivarg
                (ID, Anno, Codice, Settore, PuntoV, AliqIva, Imponibile, Iva)
            VALUES
                (@id, @year, @code, @sector, @storeCode, @vatRate, @net, @vat);
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@sector", SalesVatSector);
        command.Parameters.AddWithValue("@storeCode", storeCode);
        command.Parameters.AddWithValue("@vatRate", vatRate);
        command.Parameters.AddWithValue("@net", net);
        command.Parameters.AddWithValue("@vat", vat);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAccountingMovementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int accountingCode,
        int saleId,
        DateOnly movementDate,
        SalesEntryTotals totals,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            INSERT INTO movcont
                (Anno, Settore, Codice, DataMov, Causale, Importo, Documento)
            VALUES
                (@year, @sector, @code, @movementDate, @cause, @amount, @document);
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@sector", SalesVatSector);
        command.Parameters.AddWithValue("@code", accountingCode);
        command.Parameters.AddWithValue("@movementDate", movementDate.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@cause", SalesCause);
        command.Parameters.AddWithValue("@amount", totals.Total);
        command.Parameters.AddWithValue("@document", saleId);

        await command.ExecuteNonQueryAsync(cancellationToken);
        var id = Convert.ToInt32(command.LastInsertedId);
        var rowNumber = 0;

        await InsertAccountingRowIfPositiveAsync(
            connection,
            transaction,
            id,
            year,
            accountingCode,
            ++rowNumber,
            51,
            totals.TaxableNet,
            "A",
            cancellationToken);
        rowNumber = totals.TaxableNet > 0 ? rowNumber : rowNumber - 1;

        await InsertAccountingRowIfPositiveAsync(
            connection,
            transaction,
            id,
            year,
            accountingCode,
            ++rowNumber,
            53,
            totals.Exempt,
            "A",
            cancellationToken);
        rowNumber = totals.Exempt > 0 ? rowNumber : rowNumber - 1;

        await InsertAccountingRowIfPositiveAsync(
            connection,
            transaction,
            id,
            year,
            accountingCode,
            ++rowNumber,
            84,
            totals.Vat,
            "A",
            cancellationToken);
        rowNumber = totals.Vat > 0 ? rowNumber : rowNumber - 1;

        await InsertAccountingRowIfPositiveAsync(
            connection,
            transaction,
            id,
            year,
            accountingCode,
            ++rowNumber,
            61,
            totals.Cash,
            "D",
            cancellationToken);
        rowNumber = totals.Cash > 0 ? rowNumber : rowNumber - 1;

        await InsertAccountingRowIfPositiveAsync(
            connection,
            transaction,
            id,
            year,
            accountingCode,
            ++rowNumber,
            65,
            totals.Other,
            "D",
            cancellationToken);
        rowNumber = totals.Other > 0 ? rowNumber : rowNumber - 1;

        await InsertAccountingRowIfPositiveAsync(
            connection,
            transaction,
            id,
            year,
            accountingCode,
            ++rowNumber,
            71,
            totals.Card,
            "D",
            cancellationToken);
        rowNumber = totals.Card > 0 ? rowNumber : rowNumber - 1;

        await InsertAccountingRowIfPositiveAsync(
            connection,
            transaction,
            id,
            year,
            accountingCode,
            ++rowNumber,
            63,
            totals.Tickets,
            "D",
            cancellationToken);
        rowNumber = totals.Tickets > 0 ? rowNumber : rowNumber - 1;

        await InsertAccountingRowIfPositiveAsync(
            connection,
            transaction,
            id,
            year,
            accountingCode,
            ++rowNumber,
            62,
            totals.Checks,
            "D",
            cancellationToken);
        rowNumber = totals.Checks > 0 ? rowNumber : rowNumber - 1;

        await InsertAccountingRowIfPositiveAsync(
            connection,
            transaction,
            id,
            year,
            accountingCode,
            ++rowNumber,
            82,
            totals.Suspended,
            "D",
            cancellationToken);
        rowNumber = totals.Suspended > 0 ? rowNumber : rowNumber - 1;

        await InsertAccountingRowIfPositiveAsync(
            connection,
            transaction,
            id,
            year,
            accountingCode,
            ++rowNumber,
            32,
            totals.Losses,
            "D",
            cancellationToken);
    }

    private static async Task InsertAccountingRowIfPositiveAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int id,
        int year,
        int accountingCode,
        int rowNumber,
        int account,
        decimal amount,
        string sign,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            return;
        }

        await using var command = new MySqlCommand(
            """
            INSERT INTO movcontrg
                (ID, Anno, Settore, Codice, Riga, Conto, Importo, Segno)
            VALUES
                (@id, @year, @sector, @code, @rowNumber, @account, @amount, @sign);
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@sector", SalesVatSector);
        command.Parameters.AddWithValue("@code", accountingCode);
        command.Parameters.AddWithValue("@rowNumber", rowNumber);
        command.Parameters.AddWithValue("@account", account);
        command.Parameters.AddWithValue("@amount", amount);
        command.Parameters.AddWithValue("@sign", sign);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddSaleParameters(
        MySqlCommand command,
        int year,
        int code,
        DateOnly movementDate,
        SalesEntryTotals totals)
    {
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@movementDate", movementDate.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@cash", totals.Cash);
        command.Parameters.AddWithValue("@card", totals.Card);
        command.Parameters.AddWithValue("@tickets", totals.Tickets);
        command.Parameters.AddWithValue("@checks", totals.Checks);
        command.Parameters.AddWithValue("@other", totals.Other);
        command.Parameters.AddWithValue("@suspended", totals.Suspended);
        command.Parameters.AddWithValue("@losses", totals.Losses);
    }

    private static async Task<DateOnly?> LastRegistrationDateAsync(
        MySqlConnection connection,
        int year,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT MAX(DataMov) FROM Vendite WHERE Anno = @year;",
            connection);
        command.Parameters.AddWithValue("@year", year);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null || value == DBNull.Value)
        {
            return null;
        }

        return DateOnly.FromDateTime(Convert.ToDateTime(value));
    }

    private static async Task<IReadOnlyList<SalesEntryStoreRow>> ListStoresAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Nome, '') AS Nome
            FROM PuntiVendita
            WHERE COALESCE(Attivo, 1) <> 0
            ORDER BY Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<SalesEntryStoreRow>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new SalesEntryStoreRow(
                Convert.ToInt32(reader["Codice"]),
                reader.GetString("Nome")));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<SalesEntryStoreRow>> ListStoresForSaleAsync(
        MySqlConnection connection,
        int id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COALESCE(pv.Codice, vr.PuntoV) AS Codice,
                COALESCE(pv.Nome, '') AS Nome
            FROM VenditeRg vr
            LEFT JOIN PuntiVendita pv ON pv.Codice = vr.PuntoV
            WHERE vr.ID = @id
            ORDER BY vr.PuntoV;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        var rows = new List<SalesEntryStoreRow>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new SalesEntryStoreRow(
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Nome"]) ?? ""));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<SalesEntrySaveRow>> ListSavedRowsForSaleAsync(
        MySqlConnection connection,
        int id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT vr.PuntoV, COALESCE(pv.Nome, '') AS Nome,
                   COALESCE(mi.Imponibile, 0) AS ImponibileIva,
                   COALESCE(mi.Iva, 0) AS Iva,
                   COALESCE(mi.NonImponibile, 0) AS NonImpo,
                   vr.Carta, vr.Tickets, vr.Assegni, vr.Altro, vr.Sospesi, vr.Perdite
            FROM VenditeRg vr
            LEFT JOIN PuntiVendita pv ON pv.Codice = vr.PuntoV
            LEFT JOIN (
                SELECT Anno, Codice, PuntoV,
                       SUM(CASE WHEN COALESCE(AliqIva, 0) <> 0 OR COALESCE(Iva, 0) <> 0 THEN Imponibile ELSE 0 END) AS Imponibile,
                       SUM(CASE
                               WHEN COALESCE(Iva, 0) <> 0 THEN Iva
                               WHEN COALESCE(AliqIva, 0) <> 0 THEN ROUND(Imponibile * AliqIva / 100, 2)
                               ELSE 0
                           END) AS Iva,
                       SUM(CASE WHEN COALESCE(AliqIva, 0) = 0 AND COALESCE(Iva, 0) = 0 THEN Imponibile ELSE 0 END) AS NonImponibile
                FROM MovivaRg
                WHERE Settore = 20
                GROUP BY Anno, Codice, PuntoV
            ) mi ON mi.Anno = vr.Anno AND mi.Codice = vr.Codice AND mi.PuntoV = vr.PuntoV
            WHERE vr.ID = @id
            ORDER BY vr.PuntoV;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        var rows = new List<SalesEntrySaveRow>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var taxableNet = Money(reader["ImponibileIva"]);
            var vat = Money(reader["Iva"]);
            var exempt = Money(reader["NonImpo"]);
            var taxable = taxableNet + vat;

            rows.Add(new SalesEntrySaveRow
            {
                StoreCode = Convert.ToInt32(reader["PuntoV"]),
                StoreName = Convert.ToString(reader["Nome"]) ?? "",
                Taxable = MoneyText(taxable),
                Exempt = MoneyText(exempt),
                Card = MoneyText(Money(reader["Carta"])),
                Tickets = MoneyText(Money(reader["Tickets"])),
                Checks = MoneyText(Money(reader["Assegni"])),
                Other = MoneyText(Money(reader["Altro"])),
                Suspended = MoneyText(Money(reader["Sospesi"])),
                Losses = MoneyText(Money(reader["Perdite"]))
            });
        }

        return rows;
    }

    private sealed record SalesEntryTotals(
        decimal Exempt,
        decimal TaxableNet,
        decimal Net,
        decimal Vat,
        decimal Total,
        decimal Cash,
        decimal Card,
        decimal Tickets,
        decimal Checks,
        decimal Other,
        decimal Suspended,
        decimal Losses)
    {
        public static SalesEntryTotals FromRows(IReadOnlyList<SalesEntrySaveCommandRow> rows) =>
            new(
                rows.Sum(row => row.Exempt),
                rows.Sum(row => row.Net - row.Exempt),
                rows.Sum(row => row.Net),
                rows.Sum(row => row.Vat),
                rows.Sum(row => row.Total),
                rows.Sum(row => row.Cash),
                rows.Sum(row => row.Card),
                rows.Sum(row => row.Tickets),
                rows.Sum(row => row.Checks),
                rows.Sum(row => row.Other),
                rows.Sum(row => row.Suspended),
                rows.Sum(row => row.Losses));
    }

    private sealed record AccountingMovementKey(
        int Id,
        int Code);

    private sealed record SaleKey(
        int Id,
        int Year,
        int Code);

    private static string MoneyText(decimal value) =>
        value == 0 ? "" : value.ToString("N2", CultureInfo.GetCultureInfo("it-IT"));

    private static decimal Money(object value) =>
        value is null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
}

