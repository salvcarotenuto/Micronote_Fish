using System.Globalization;
using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class CounterSaleRepository(MicronoteDb database)
{
    public async Task<(decimal Price, decimal VatRate)?> LoadLastPriceAsync(
        int customerCode,
        string articleCode,
        CancellationToken cancellationToken = default)
    {
        if (customerCode <= 0 || string.IsNullOrWhiteSpace(articleCode))
            return null;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT vr.Prezzo, vr.AliqIva AS Iva
            FROM VenditeRg vr
            INNER JOIN Vendite v ON v.ID=vr.ID
            WHERE v.Cliente=@customer AND vr.Articolo=@article
            ORDER BY v.DataDoc DESC, v.ID DESC, vr.Riga DESC
            LIMIT 1;
            """, connection);
        command.Parameters.AddWithValue("@customer", customerCode);
        command.Parameters.AddWithValue("@article", articleCode.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return (Convert.ToDecimal(reader["Prezzo"]), Convert.ToDecimal(reader["Iva"]));
    }

    public async Task<CounterSalePageData> LoadAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var inventoryYear = await LoadInventoryYearAsync(connection, cancellationToken);
        return new CounterSalePageData
        {
            Year = year,
            Date = DateOnly.FromDateTime(DateTime.Today),
            EnableAmountEditing = await LoadAmountEditingOptionAsync(connection, cancellationToken),
            InitialGrouping = await LoadInitialGroupingOptionAsync(connection, cancellationToken),
            Articles = await LoadArticlesAsync(connection, inventoryYear, cancellationToken),
            Customers = await LoadCustomersAsync(connection, cancellationToken)
        };
    }

    public async Task<CounterSaleSaveResult> SaveAsync(
        int year,
        int userCode,
        CounterSaleSaveModel sale,
        CancellationToken cancellationToken = default)
    {
        if (sale.CustomerCode <= 0 || sale.Rows.Count < 1)
        {
            throw new InvalidOperationException(
                "Per salvare occorre selezionare un cliente e inserire almeno una riga di vendita.");
        }

        foreach (var row in sale.Rows)
        {
            ValidateSaleRow(row);
        }

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var storeCode = await LoadCustomerAsync(
                connection, transaction, sale.CustomerCode, cancellationToken)
                ?? throw new InvalidOperationException("Cliente non trovato o non attivo.");
            var code = await NextCodeAsync(connection, transaction, year, cancellationToken);
            var merchandise = decimal.Round(sale.Rows.Sum(NetAmount), 2);
            var vat = decimal.Round(sale.Rows.Sum(row => row.Amount) - merchandise, 2);
            var gross = decimal.Round(sale.Rows.Sum(row => row.Amount), 2);
            if (gross is < -9999999999.99m or > 9999999999.99m)
                throw new InvalidOperationException(
                    "La somma delle righe supera la dimensione consentita per Vendite.Totale: massimo 9.999.999.999,99.");
            var discount = decimal.Round(sale.Discount, 2);
            var paidAmount = decimal.Round(sale.PaidAmount, 2);
            if (discount < 0 || discount > gross || discount != sale.Discount)
                throw new InvalidOperationException("L'abbuono deve essere compreso tra zero e il totale vendita, con massimo 2 decimali.");
            if (paidAmount < 0 || paidAmount > 9999999999.99m || paidAmount != sale.PaidAmount)
                throw new InvalidOperationException("L'importo pagato deve essere positivo e ammette al massimo 10 interi e 2 decimali.");
            var total = gross - discount;

            await using var header = new MySqlCommand(
                """
                INSERT INTO Vendite
                    (Anno, Codice, Stato, NumDoc, DataDoc, Cliente, Merce,
                     Agente, Provvigione, Iva, Totale, Abbuono, Pagato, PuntoV)
                VALUES
                    (@year, @code, 0, @code, @date, @customer, @merchandise,
                     0, 0, @vat, @total, @discount, @paidAmount, @store);
                """, connection, transaction);
            header.Parameters.AddWithValue("@year", year);
            header.Parameters.AddWithValue("@code", code);
            header.Parameters.Add("@date", MySqlDbType.Date).Value = DateTime.Today;
            header.Parameters.AddWithValue("@customer", sale.CustomerCode);
            header.Parameters.AddWithValue("@merchandise", merchandise);
            header.Parameters.AddWithValue("@vat", vat);
            header.Parameters.AddWithValue("@total", total);
            header.Parameters.AddWithValue("@discount", discount);
            header.Parameters.AddWithValue("@paidAmount", paidAmount);
            header.Parameters.AddWithValue("@store", storeCode);
            await header.ExecuteNonQueryAsync(cancellationToken);
            var id = checked((int)header.LastInsertedId);

            for (var index = 0; index < sale.Rows.Count; index++)
            {
                var row = sale.Rows[index];
                await ValidateArticleAsync(connection, transaction, row.ArticleCode, cancellationToken);
                await using var detail = new MySqlCommand(
                    """
                    INSERT INTO VenditeRg
                        (ID, Riga, Articolo, Ums, Colli, Tara, Quantita,
                         Prezzo, AliqIva, Importo)
                    VALUES
                        (@id, @row, @article, @unit, @packages, @tare, @quantity,
                         @price, @vat, @amount);
                    """, connection, transaction);
                detail.Parameters.AddWithValue("@id", id);
                detail.Parameters.AddWithValue("@row", index + 1);
                detail.Parameters.AddWithValue("@article", row.ArticleCode.Trim());
                detail.Parameters.AddWithValue("@unit", row.Unit.Trim());
                detail.Parameters.AddWithValue("@packages", row.Packages);
                detail.Parameters.AddWithValue("@tare", decimal.Round(row.Tare, 3));
                detail.Parameters.AddWithValue("@quantity", decimal.Round(row.Quantity, 3));
                detail.Parameters.AddWithValue("@price", decimal.Round(row.Price, 2));
                detail.Parameters.AddWithValue("@vat", decimal.Round(row.VatRate, 2));
                detail.Parameters.AddWithValue("@amount", decimal.Round(row.Amount, 2));
                await detail.ExecuteNonQueryAsync(cancellationToken);
            }

            await InsertStockMovementsAsync(
                connection,
                transaction,
                year,
                code,
                sale.CustomerCode,
                storeCode,
                sale.Rows,
                cancellationToken);

            if (paidAmount != 0)
            {
                await InsertReceiptAsync(
                    connection,
                    transaction,
                    year,
                    code,
                    id,
                    sale.CustomerCode,
                    storeCode,
                    paidAmount,
                    cancellationToken);
            }

            await DeleteDraftAsync(
                connection, transaction, year, userCode, sale.DraftId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new CounterSaleSaveResult(id, code);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<CounterSaleSaveModel?> LoadDraftAsync(
        int year,
        int userCode,
        CancellationToken cancellationToken = default)
    {
        if (userCode <= 0)
            return null;
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await EnsureDraftTableAsync(connection, cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT VenditaID, Riga, Cliente, Articolo, Ums, Colli, Tara, Quantita,
                   Prezzo, Iva, Importo
            FROM VenditaTmp
            WHERE Anno=@year AND Utente=@user
              AND VenditaID=(
                  SELECT selected.VenditaID
                  FROM (
                      SELECT VenditaID
                      FROM VenditaTmp
                      WHERE Anno=@year AND Utente=@user
                      ORDER BY Aggiornato DESC, VenditaID DESC
                      LIMIT 1
                  ) selected
              )
            ORDER BY Riga;
            """,
            connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@user", userCode);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        CounterSaleSaveModel? draft = null;
        while (await reader.ReadAsync(cancellationToken))
        {
            var rowNumber = Convert.ToInt32(reader["Riga"]);
            draft ??= new CounterSaleSaveModel();
            draft.DraftId = Convert.ToInt64(reader["VenditaID"]);
            if (rowNumber == 0)
            {
                draft.CustomerCode = Convert.ToInt32(reader["Cliente"]);
                continue;
            }
            draft.Rows.Add(new CounterSaleSaveRow
            {
                ArticleCode = Convert.ToString(reader["Articolo"]) ?? "",
                Unit = Convert.ToString(reader["Ums"]) ?? "",
                Packages = Convert.ToInt32(reader["Colli"]),
                Tare = Convert.ToDecimal(reader["Tara"]),
                Quantity = Convert.ToDecimal(reader["Quantita"]),
                Price = Convert.ToDecimal(reader["Prezzo"]),
                VatRate = Convert.ToDecimal(reader["Iva"]),
                Amount = Convert.ToDecimal(reader["Importo"])
            });
        }
        return draft;
    }

    public async Task<long> SaveDraftAsync(
        int year,
        int userCode,
        CounterSaleSaveModel draft,
        CancellationToken cancellationToken = default)
    {
        if (userCode <= 0)
            throw new InvalidOperationException("Utente non identificato.");
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await EnsureDraftTableAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await DeleteDraftAsync(
                connection, transaction, year, userCode, draft.DraftId, cancellationToken);
            var draftId = draft.DraftId > 0
                ? draft.DraftId
                : await NextDraftIdAsync(
                    connection, transaction, year, userCode, cancellationToken);
            if (draft.CustomerCode > 0 || draft.Rows.Count > 0)
            {
                await InsertDraftRowAsync(
                    connection, transaction, year, userCode, draftId, 0,
                    draft.CustomerCode, null, cancellationToken);
                for (var index = 0; index < draft.Rows.Count; index++)
                {
                    await InsertDraftRowAsync(
                        connection, transaction, year, userCode, draftId, index + 1,
                        draft.CustomerCode, draft.Rows[index], cancellationToken);
                }
            }
            await transaction.CommitAsync(cancellationToken);
            return draftId;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeleteDraftAsync(
        int year,
        int userCode,
        long draftId,
        CancellationToken cancellationToken = default)
    {
        if (userCode <= 0)
            return;
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await EnsureDraftTableAsync(connection, cancellationToken);
        await using var command = new MySqlCommand(
            """
            DELETE FROM VenditaTmp
            WHERE Anno=@year AND Utente=@user
              AND (@draftId=0 OR VenditaID=@draftId);
            """,
            connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@user", userCode);
        command.Parameters.AddWithValue("@draftId", draftId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureDraftTableAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            CREATE TABLE IF NOT EXISTS VenditaTmp
            (
                ID BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                VenditaID BIGINT UNSIGNED NOT NULL,
                Anno SMALLINT UNSIGNED NOT NULL,
                Utente SMALLINT UNSIGNED NOT NULL,
                Riga SMALLINT UNSIGNED NOT NULL,
                Codice INT UNSIGNED NOT NULL DEFAULT 0,
                DataDoc DATE NOT NULL,
                Cliente INT UNSIGNED NOT NULL DEFAULT 0,
                PuntoV SMALLINT UNSIGNED NOT NULL DEFAULT 0,
                Merce DECIMAL(12,2) NOT NULL DEFAULT 0,
                IvaTotale DECIMAL(12,2) NOT NULL DEFAULT 0,
                Totale DECIMAL(12,2) NOT NULL DEFAULT 0,
                Abbuono DECIMAL(10,2) NOT NULL DEFAULT 0,
                ImportoPagato DECIMAL(12,2) NOT NULL DEFAULT 0,
                Articolo VARCHAR(30) NOT NULL DEFAULT '',
                Ums VARCHAR(10) NOT NULL DEFAULT '',
                Colli SMALLINT NOT NULL DEFAULT 0,
                Tara DECIMAL(10,3) NOT NULL DEFAULT 0,
                Quantita DECIMAL(10,3) NOT NULL DEFAULT 0,
                Prezzo DECIMAL(10,2) NOT NULL DEFAULT 0,
                Iva DECIMAL(5,2) NOT NULL DEFAULT 0,
                Importo DECIMAL(12,2) NOT NULL DEFAULT 0,
                Aggiornato DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                    ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (ID),
                UNIQUE KEY UX_VenditaTmp_Logica
                    (VenditaID, Anno, Utente, Riga),
                INDEX IX_VenditaTmp_Utente
                    (Anno, Utente, VenditaID),
                INDEX IX_VenditaTmp_Aggiornato (Aggiornato)
            );
            """,
            connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var inspect = new MySqlCommand(
            """
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=DATABASE()
              AND TABLE_NAME='VenditaTmp'
              AND COLUMN_NAME='ID';
            """,
            connection);
        if (Convert.ToInt32(await inspect.ExecuteScalarAsync(cancellationToken)) == 0)
        {
            await using var upgrade = new MySqlCommand(
                """
                ALTER TABLE VenditaTmp
                    DROP PRIMARY KEY,
                    ADD COLUMN ID BIGINT UNSIGNED NOT NULL AUTO_INCREMENT FIRST,
                    ADD COLUMN VenditaID BIGINT UNSIGNED NOT NULL DEFAULT 1 AFTER ID,
                    ADD COLUMN Codice INT UNSIGNED NOT NULL DEFAULT 0 AFTER Riga,
                    ADD COLUMN DataDoc DATE NULL AFTER Codice,
                    ADD COLUMN PuntoV SMALLINT UNSIGNED NOT NULL DEFAULT 0 AFTER Cliente,
                    ADD COLUMN Merce DECIMAL(12,2) NOT NULL DEFAULT 0 AFTER PuntoV,
                    ADD COLUMN IvaTotale DECIMAL(12,2) NOT NULL DEFAULT 0 AFTER Merce,
                    ADD COLUMN Totale DECIMAL(12,2) NOT NULL DEFAULT 0 AFTER IvaTotale,
                    ADD COLUMN Abbuono DECIMAL(10,2) NOT NULL DEFAULT 0 AFTER Totale,
                    ADD COLUMN ImportoPagato DECIMAL(12,2) NOT NULL DEFAULT 0 AFTER Abbuono,
                    ADD PRIMARY KEY (ID),
                    ADD UNIQUE KEY UX_VenditaTmp_Logica
                        (VenditaID, Anno, Utente, Riga),
                    ADD INDEX IX_VenditaTmp_Utente
                        (Anno, Utente, VenditaID);
                UPDATE VenditaTmp SET DataDoc=CURDATE() WHERE DataDoc IS NULL;
                ALTER TABLE VenditaTmp MODIFY DataDoc DATE NOT NULL;
                """,
                connection);
            await upgrade.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertDraftRowAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int userCode,
        long draftId,
        int rowNumber,
        int customerCode,
        CounterSaleSaveRow? row,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            INSERT INTO VenditaTmp
                (VenditaID, Anno, Utente, Riga, Codice, DataDoc,
                 Cliente, PuntoV, Merce, IvaTotale, Totale, Abbuono,
                 ImportoPagato, Articolo, Ums, Colli, Tara, Quantita,
                 Prezzo, Iva, Importo)
            VALUES
                (@draftId, @year, @user, @row, 0, @date,
                 @customer, 0, 0, 0, 0, 0,
                 0, @article, @unit, @packages, @tare, @quantity,
                 @price, @vat, @amount);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@draftId", draftId);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@user", userCode);
        command.Parameters.AddWithValue("@row", rowNumber);
        command.Parameters.Add("@date", MySqlDbType.Date).Value = DateTime.Today;
        command.Parameters.AddWithValue("@customer", customerCode);
        command.Parameters.AddWithValue("@article", row?.ArticleCode.Trim() ?? "");
        command.Parameters.AddWithValue("@unit", row?.Unit.Trim() ?? "");
        command.Parameters.AddWithValue("@packages", row?.Packages ?? 0);
        command.Parameters.AddWithValue("@tare", decimal.Round(row?.Tare ?? 0, 3));
        command.Parameters.AddWithValue("@quantity", decimal.Round(row?.Quantity ?? 0, 3));
        command.Parameters.AddWithValue("@price", decimal.Round(row?.Price ?? 0, 2));
        command.Parameters.AddWithValue("@vat", decimal.Round(row?.VatRate ?? 0, 2));
        command.Parameters.AddWithValue("@amount", decimal.Round(row?.Amount ?? 0, 2));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteDraftAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int userCode,
        long draftId,
        CancellationToken cancellationToken)
    {
        if (userCode <= 0)
            return;
        await using var command = new MySqlCommand(
            """
            DELETE FROM VenditaTmp
            WHERE Anno=@year AND Utente=@user
              AND (@draftId=0 OR VenditaID=@draftId);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@user", userCode);
        command.Parameters.AddWithValue("@draftId", draftId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> NextDraftIdAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int userCode,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT COALESCE(MAX(VenditaID),0)+1
            FROM VenditaTmp
            WHERE Anno=@year AND Utente=@user
            FOR UPDATE;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@user", userCode);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static decimal NetAmount(CounterSaleSaveRow row)
    {
        var quantity = row.Quantity != 0 ? row.Quantity : row.Packages;
        return decimal.Round(quantity * row.Price, 2);
    }

    private static void ValidateSaleRow(CounterSaleSaveRow row)
    {
        if (row.Packages is < -32000 or > 32000)
            throw new InvalidOperationException("Il numero dei colli deve essere compreso tra -32000 e 32000.");
        if (row.Quantity is < -999999.999m or > 999999.999m
            || decimal.Round(row.Quantity, 3) != row.Quantity)
            throw new InvalidOperationException("Il peso ammette al massimo 6 interi e 3 decimali.");
        if (row.Price is < 0 or > 999999.99m
            || decimal.Round(row.Price, 2) != row.Price)
            throw new InvalidOperationException("Il prezzo ammette al massimo 6 interi e 2 decimali senza segno.");
        if (row.VatRate is < 0 or > 100
            || decimal.Round(row.VatRate, 2) != row.VatRate)
            throw new InvalidOperationException("L'IVA deve essere compresa tra 0 e 100 con massimo 2 decimali.");
        if (row.Amount is < -9999999999.99m or > 9999999999.99m
            || decimal.Round(row.Amount, 2) != row.Amount)
            throw new InvalidOperationException(
                "L'importo totale della riga supera la dimensione consentita: massimo 9.999.999.999,99 con 2 decimali.");
    }

    private static async Task InsertStockMovementsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int code,
        int customerCode,
        int storeCode,
        IReadOnlyList<CounterSaleSaveRow> rows,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var movementQuantity = row.Quantity != 0
                ? row.Quantity
                : row.Packages;
            var vatPrice = decimal.Round(
                row.Price * (1 + row.VatRate / 100),
                2);
            await using var movement = new MySqlCommand(
                """
                INSERT INTO Movimenti
                    (Anno, Settore, Codice, Riga, Causale, NumDoc, DataMov,
                     CliFor, Ditta, TipoMov, Articolo, Colli, Quantita,
                     Prezzo, Importo, PuntoV)
                VALUES
                    (@year, 20, @code, @row, 20, @documentNumber, @date,
                     'C', @customer, 'S', @article, @packages, @quantity,
                     @price, @amount, @store);
                """,
                connection,
                transaction);
            movement.Parameters.AddWithValue("@year", year);
            movement.Parameters.AddWithValue("@code", code);
            movement.Parameters.AddWithValue("@row", index + 1);
            movement.Parameters.AddWithValue("@documentNumber", code);
            movement.Parameters.Add("@date", MySqlDbType.Date).Value = DateTime.Today;
            movement.Parameters.AddWithValue("@customer", customerCode);
            movement.Parameters.AddWithValue("@article", row.ArticleCode.Trim());
            movement.Parameters.AddWithValue("@packages", row.Packages);
            movement.Parameters.AddWithValue("@quantity", decimal.Round(movementQuantity, 3));
            movement.Parameters.AddWithValue("@price", vatPrice);
            movement.Parameters.AddWithValue("@amount", decimal.Round(row.Amount, 2));
            movement.Parameters.AddWithValue("@store", storeCode);
            await movement.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertReceiptAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int saleCode,
        int saleId,
        int customerCode,
        int storeCode,
        decimal paidAmount,
        CancellationToken cancellationToken)
    {
        await using var nextCode = new MySqlCommand(
            """
            SELECT COALESCE(MAX(Codice),0)+1
            FROM MovCont
            WHERE Anno=@year AND Settore=42
            FOR UPDATE;
            """,
            connection,
            transaction);
        nextCode.Parameters.AddWithValue("@year", year);
        var receiptCode = Convert.ToInt32(
            await nextCode.ExecuteScalarAsync(cancellationToken));

        await using var receipt = new MySqlCommand(
            """
            INSERT INTO MovCont
                (Anno, Settore, Codice, Causale, DataMov, CliFor, Ditta,
                 NumDoc, TipoPag, Titolo, Documento, Importo, PuntoV, Note)
            VALUES
                (@year, 42, @code, 20, @date, 'C', @customer,
                 @documentNumber, '', '', @saleId, @amount, @store, @note);
            """,
            connection,
            transaction);
        receipt.Parameters.AddWithValue("@year", year);
        receipt.Parameters.AddWithValue("@code", receiptCode);
        receipt.Parameters.Add("@date", MySqlDbType.Date).Value = DateTime.Today;
        receipt.Parameters.AddWithValue("@customer", customerCode);
        receipt.Parameters.AddWithValue("@documentNumber", saleCode);
        receipt.Parameters.AddWithValue("@saleId", saleId);
        receipt.Parameters.AddWithValue("@amount", paidAmount);
        receipt.Parameters.AddWithValue("@store", storeCode);
        receipt.Parameters.AddWithValue(
            "@note",
            $"Incasso vendita al banco n. {saleCode:000000}");
        await receipt.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<CounterSaleArticle>> LoadArticlesAsync(
        MySqlConnection connection,
        int inventoryYear,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT a.Codice, COALESCE(a.Descrizione,'') Descrizione,
                   COALESCE(a.Umv,'') Ums,
                   COALESCE(a.Categoria,0) Categoria, COALESCE(c.Descrizione,'') CategoriaNome,
                   COALESCE(a.Gruppo,0) Gruppo, COALESCE(g.Descrizione,'') GruppoNome,
                   COALESCE(a.Specie,0) Specie, COALESCE(s.Descrizione,'') SpecieNome,
                   COALESCE(a.Provenienza,0) Provenienza, COALESCE(p.Descrizione,'') ProvenienzaNome,
                   COALESCE(a.Tara,0) Tara,
                   (CASE WHEN @inventoryYear>0 THEN COALESCE(a.GiacinP,0) ELSE 0 END
                    + COALESCE(m.Movimento,0)) Giacenza,
                   COALESCE(a.PrezzoStd,0) Prezzo, COALESCE(a.AliqIva,0) Iva
            FROM Articoli a
            LEFT JOIN Categorie c ON c.Codice=a.Categoria
            LEFT JOIN Gruppi g ON g.Codice=a.Gruppo
            LEFT JOIN Specie s ON s.Codice=a.Specie
            LEFT JOIN Provenienza p ON p.Codice=a.Provenienza
            LEFT JOIN (
                SELECT Articolo,
                       SUM(CASE WHEN TipoMov='C' THEN COALESCE(Quantita,0) ELSE 0 END)
                       - SUM(CASE WHEN TipoMov='S' THEN COALESCE(Quantita,0) ELSE 0 END) Movimento
                FROM Movimenti
                WHERE Anno>=@inventoryYear
                GROUP BY Articolo
            ) m ON m.Articolo=a.Codice
            ORDER BY a.Descrizione, a.Codice;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@inventoryYear", inventoryYear);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<CounterSaleArticle>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CounterSaleArticle(
                Convert.ToString(reader["Codice"]) ?? "",
                Convert.ToString(reader["Descrizione"]) ?? "",
                Convert.ToString(reader["Ums"]) ?? "",
                Convert.ToInt32(reader["Categoria"]),
                Convert.ToString(reader["CategoriaNome"]) ?? "",
                Convert.ToInt32(reader["Gruppo"]),
                Convert.ToString(reader["GruppoNome"]) ?? "",
                Convert.ToInt32(reader["Specie"]),
                Convert.ToString(reader["SpecieNome"]) ?? "",
                Convert.ToInt32(reader["Provenienza"]),
                Convert.ToString(reader["ProvenienzaNome"]) ?? "",
                Convert.ToDecimal(reader["Tara"]),
                Convert.ToDecimal(reader["Giacenza"]),
                Convert.ToDecimal(reader["Prezzo"]),
                Convert.ToDecimal(reader["Iva"])));
        }
        return rows;
    }

    private static async Task<int> LoadInventoryYearAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT COALESCE(Valore,'')
            FROM Opzioni
            WHERE Chiave='DataInventario'
            LIMIT 1;
            """, connection);
        var value = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken))?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        string[] supportedFormats = ["yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "yyyyMMdd"];
        if (DateOnly.TryParseExact(
                value,
                supportedFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var inventoryDate))
        {
            return inventoryDate.Year;
        }

        return int.TryParse(value, out var legacyYear) && legacyYear is >= 1900 and <= 9999
            ? legacyYear
            : 0;
    }

    private static async Task<bool> LoadAmountEditingOptionAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT COALESCE(MAX(Valore),'0')
            FROM Opzioni
            WHERE Chiave='AttivaImporto';
            """, connection);
        var value = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken))?.Trim();
        return value == "1";
    }

    private static async Task<string> LoadInitialGroupingOptionAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT COALESCE(MAX(Valore),'category')
            FROM Opzioni
            WHERE Chiave='RaggruppamentoVenditaBanco';
            """, connection);
        var value = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken))?.Trim();
        return value is "category" or "group" or "species" or "origin"
            ? value
            : "category";
    }

    private static async Task<IReadOnlyList<CounterSaleCustomer>> LoadCustomersAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT Codice, COALESCE(Nome,'') Nome, COALESCE(Citta,'') Citta,
                   COALESCE(PuntoV,0) PuntoV
            FROM Clienti WHERE COALESCE(Attivo,1)<>0 ORDER BY Nome, Codice;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<CounterSaleCustomer>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new(Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Nome"]) ?? "",
                Convert.ToString(reader["Citta"]) ?? "",
                Convert.ToInt32(reader["PuntoV"])));
        return rows;
    }

    private static async Task<int?> LoadCustomerAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int code,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COALESCE(PuntoV,0) FROM Clienti WHERE Codice=@code AND COALESCE(Attivo,1)<>0;",
            connection, transaction);
        command.Parameters.AddWithValue("@code", code);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? null : Convert.ToInt32(value);
    }

    private static async Task<int> NextCodeAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COALESCE(MAX(Codice),0)+1 FROM Vendite WHERE Anno=@year FOR UPDATE;",
            connection, transaction);
        command.Parameters.AddWithValue("@year", year);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task ValidateArticleAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string code,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM Articoli WHERE Codice=@code;",
            connection, transaction);
        command.Parameters.AddWithValue("@code", code.Trim());
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new InvalidOperationException($"Articolo {code} non trovato.");
    }
}
