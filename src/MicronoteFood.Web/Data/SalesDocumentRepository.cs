using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class SalesDocumentRepository(MicronoteDb database)
{
    public async Task<SalesDocumentPageData> LoadAsync(
        int year,
        int? id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        if (id is null)
        {
            return new SalesDocumentPageData
            {
                Year = year,
                Code = await NextCodeAsync(connection, null, year, false, cancellationToken),
                DocumentDate = DateOnly.FromDateTime(DateTime.Today)
            };
        }

        const string headerSql = """
            SELECT v.ID, v.Anno, v.Codice, v.DataDoc, v.Cliente,
                   COALESCE(c.Nome, '') AS ClienteNome,
                   COALESCE(v.PuntoV, 0) AS PuntoV,
                   COALESCE(v.Abbuono, 0) AS Abbuono,
                   COALESCE(v.Pagato, 0) AS Pagato
            FROM Vendite v
            LEFT JOIN Clienti c ON c.Codice = v.Cliente
            WHERE v.ID = @id
            LIMIT 1;
            """;
        SalesDocumentPageData? document = null;
        await using (var command = new MySqlCommand(headerSql, connection))
        {
            command.Parameters.AddWithValue("@id", id.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                document = new SalesDocumentPageData
                {
                    Id = Convert.ToInt32(reader["ID"]),
                    Year = Convert.ToInt32(reader["Anno"]),
                    Code = Convert.ToInt32(reader["Codice"]),
                    DocumentDate = reader["DataDoc"] is DBNull
                        ? DateOnly.FromDateTime(DateTime.Today)
                        : DateOnly.FromDateTime(Convert.ToDateTime(reader["DataDoc"])),
                    CustomerCode = Convert.ToInt32(reader["Cliente"]),
                    CustomerName = Convert.ToString(reader["ClienteNome"]) ?? "",
                    StoreCode = Convert.ToInt32(reader["PuntoV"]),
                    Discount = Convert.ToDecimal(reader["Abbuono"]),
                    Paid = Convert.ToDecimal(reader["Pagato"])
                };
            }
        }
        if (document is null)
            throw new InvalidOperationException("Vendita non trovata.");

        const string rowsSql = """
            SELECT vr.Riga, vr.Articolo, COALESCE(a.Descrizione, '') AS Descrizione,
                   vr.Ums, vr.Colli, vr.Tara, vr.Quantita, vr.Prezzo, vr.Iva, vr.Importo
            FROM VenditeRg vr
            LEFT JOIN Articoli a ON a.Codice = vr.Articolo
            WHERE vr.ID = @id
            ORDER BY vr.Riga;
            """;
        var rows = new List<SalesDocumentRow>();
        await using (var command = new MySqlCommand(rowsSql, connection))
        {
            command.Parameters.AddWithValue("@id", id.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new(
                    Convert.ToInt32(reader["Riga"]),
                    Convert.ToString(reader["Articolo"]) ?? "",
                    Convert.ToString(reader["Descrizione"]) ?? "",
                    Convert.ToString(reader["Ums"]) ?? "",
                    Convert.ToInt32(reader["Colli"]),
                    Convert.ToDecimal(reader["Tara"]),
                    Convert.ToDecimal(reader["Quantita"]),
                    Convert.ToDecimal(reader["Prezzo"]),
                    Convert.ToDecimal(reader["Iva"]),
                    Convert.ToDecimal(reader["Importo"])));
            }
        }
        return new SalesDocumentPageData
        {
            Id = document.Id,
            Year = document.Year,
            Code = document.Code,
            DocumentDate = document.DocumentDate,
            CustomerCode = document.CustomerCode,
            CustomerName = document.CustomerName,
            StoreCode = document.StoreCode,
            Discount = document.Discount,
            Paid = document.Paid,
            Rows = rows
        };
    }

    public async Task<SalesDocumentSaveResult> SaveAsync(
        int year,
        SalesDocumentSaveModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.CustomerCode <= 0)
            throw new InvalidOperationException("Selezionare il cliente.");
        if (model.Rows.Count == 0)
            throw new InvalidOperationException("Inserire almeno una riga articolo.");
        if (model.DocumentDate.Year != year)
            throw new InvalidOperationException("La data documento non appartiene all'esercizio corrente.");

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var customerStore = await CustomerStoreAsync(
                connection, transaction, model.CustomerCode, cancellationToken);
            var store = model.StoreCode > 0 ? model.StoreCode : customerStore;
            var code = model.Id is null
                ? await NextCodeAsync(connection, transaction, year, true, cancellationToken)
                : await ExistingCodeAsync(connection, transaction, model.Id.Value, cancellationToken);
            var merchandise = decimal.Round(model.Rows.Sum(NetAmount), 2);
            var gross = decimal.Round(model.Rows.Sum(row => row.Amount), 2);
            var vat = gross - merchandise;
            var discount = decimal.Round(model.Discount, 2);
            var paid = decimal.Round(model.Paid, 2);
            if (discount < 0 || discount > gross)
                throw new InvalidOperationException("L'abbuono non è valido.");
            if (paid < 0)
                throw new InvalidOperationException("L'importo pagato non è valido.");
            var total = gross - discount;

            int id;
            if (model.Id is null)
            {
                await using var command = new MySqlCommand("""
                    INSERT INTO Vendite
                        (Anno,Codice,Stato,NumDoc,DataDoc,Cliente,Merce,Agente,
                         Provvigione,Iva,Totale,Abbuono,Pagato,PuntoV)
                    VALUES
                        (@year,@code,0,@code,@date,@customer,@goods,0,
                         0,@vat,@total,@discount,@paid,@store);
                    """, connection, transaction);
                AddHeaderParameters(command, year, code, model, merchandise, vat, total, discount, paid, store);
                await command.ExecuteNonQueryAsync(cancellationToken);
                id = checked((int)command.LastInsertedId);
            }
            else
            {
                id = model.Id.Value;
                await using var command = new MySqlCommand("""
                    UPDATE Vendite SET
                        DataDoc=@date, Cliente=@customer, Merce=@goods, Iva=@vat,
                        Totale=@total, Abbuono=@discount, Pagato=@paid, PuntoV=@store
                    WHERE ID=@id;
                    """, connection, transaction);
                AddHeaderParameters(command, year, code, model, merchandise, vat, total, discount, paid, store);
                command.Parameters.AddWithValue("@id", id);
                await command.ExecuteNonQueryAsync(cancellationToken);
                await DeleteChildrenAsync(connection, transaction, id, year, code, cancellationToken);
            }

            for (var index = 0; index < model.Rows.Count; index++)
            {
                var row = model.Rows[index];
                ValidateRow(row);
                await InsertRowAsync(connection, transaction, id, index + 1, row, cancellationToken);
                await InsertMovementAsync(
                    connection, transaction, year, code, model.CustomerCode, store,
                    model.DocumentDate, index + 1, row, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return new(id, code);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<SalesEntryStoreRow>> ListStoresAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT Codice, COALESCE(Nome, '') AS Nome
            FROM PuntiVendita
            ORDER BY Nome, Codice;
            """;
        var rows = new List<SalesEntryStoreRow>();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new(
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Nome"]) ?? ""));
        return rows;
    }

    private static decimal NetAmount(SalesDocumentSaveRow row) =>
        row.VatRate == 0
            ? row.Amount
            : decimal.Round(row.Amount / (1 + row.VatRate / 100), 2);

    private static void ValidateRow(SalesDocumentSaveRow row)
    {
        if (string.IsNullOrWhiteSpace(row.ArticleCode))
            throw new InvalidOperationException("Codice articolo mancante.");
        if (row.Packages == 0 && row.Quantity == 0)
            throw new InvalidOperationException($"Indicare colli o quantità per l'articolo {row.ArticleCode}.");
        if (row.Price == 0)
            throw new InvalidOperationException($"Indicare il prezzo per l'articolo {row.ArticleCode}.");
        if (row.VatRate is < 0 or > 100)
            throw new InvalidOperationException($"Aliquota IVA non valida per l'articolo {row.ArticleCode}.");
    }

    private static async Task<int> CustomerStoreAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int customer,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COALESCE(PuntoV,0) FROM Clienti WHERE Codice=@code LIMIT 1;",
            connection, transaction);
        command.Parameters.AddWithValue("@code", customer);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null)
            throw new InvalidOperationException("Cliente non trovato.");
        return Convert.ToInt32(value);
    }

    private static async Task<int> ExistingCodeAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int id,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT Codice FROM Vendite WHERE ID=@id FOR UPDATE;",
            connection, transaction);
        command.Parameters.AddWithValue("@id", id);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null
            ? throw new InvalidOperationException("Vendita non trovata.")
            : Convert.ToInt32(value);
    }

    private static async Task<int> NextCodeAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        int year,
        bool lockRows,
        CancellationToken cancellationToken)
    {
        var suffix = lockRows ? " FOR UPDATE" : "";
        await using var command = new MySqlCommand(
            $"SELECT COALESCE(MAX(Codice),0)+1 FROM Vendite WHERE Anno=@year{suffix};",
            connection, transaction);
        command.Parameters.AddWithValue("@year", year);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static void AddHeaderParameters(
        MySqlCommand command,
        int year,
        int code,
        SalesDocumentSaveModel model,
        decimal goods,
        decimal vat,
        decimal total,
        decimal discount,
        decimal paid,
        int store)
    {
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.Add("@date", MySqlDbType.Date).Value =
            model.DocumentDate.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@customer", model.CustomerCode);
        command.Parameters.AddWithValue("@goods", goods);
        command.Parameters.AddWithValue("@vat", vat);
        command.Parameters.AddWithValue("@total", total);
        command.Parameters.AddWithValue("@discount", discount);
        command.Parameters.AddWithValue("@paid", paid);
        command.Parameters.AddWithValue("@store", store);
    }

    private static async Task DeleteChildrenAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int id,
        int year,
        int code,
        CancellationToken cancellationToken)
    {
        await using (var command = new MySqlCommand(
            "DELETE FROM VenditeRg WHERE ID=@id;", connection, transaction))
        {
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var command = new MySqlCommand(
            "DELETE FROM Movimenti WHERE Anno=@year AND Settore=20 AND Codice=@code;",
            connection, transaction))
        {
            command.Parameters.AddWithValue("@year", year);
            command.Parameters.AddWithValue("@code", code);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertRowAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int id,
        int number,
        SalesDocumentSaveRow row,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand("""
            INSERT INTO VenditeRg
                (ID,Riga,Articolo,Ums,Colli,Tara,Quantita,Prezzo,Iva,Importo)
            VALUES
                (@id,@row,@article,@unit,@packages,@tare,@quantity,@price,@vat,@amount);
            """, connection, transaction);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@row", number);
        AddRowParameters(command, row);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertMovementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int code,
        int customer,
        int store,
        DateOnly documentDate,
        int number,
        SalesDocumentSaveRow row,
        CancellationToken cancellationToken)
    {
        var quantity = row.Quantity != 0 ? row.Quantity : row.Packages;
        var vatPrice = decimal.Round(row.Price * (1 + row.VatRate / 100), 2);
        await using var command = new MySqlCommand("""
            INSERT INTO Movimenti
                (Anno,Settore,Codice,Riga,Causale,NumDoc,DataMov,
                 CliFor,Ditta,TipoMov,Articolo,Colli,Quantita,Prezzo,Importo,PuntoV)
            VALUES
                (@year,20,@code,@row,20,@code,@date,
                 'C',@customer,'S',@article,@packages,@quantity,@price,@amount,@store);
            """, connection, transaction);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@row", number);
        command.Parameters.Add("@date", MySqlDbType.Date).Value =
            documentDate.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@customer", customer);
        command.Parameters.AddWithValue("@store", store);
        command.Parameters.AddWithValue("@article", row.ArticleCode.Trim());
        command.Parameters.AddWithValue("@packages", row.Packages);
        command.Parameters.AddWithValue("@quantity", decimal.Round(quantity, 3));
        command.Parameters.AddWithValue("@price", vatPrice);
        command.Parameters.AddWithValue("@amount", decimal.Round(row.Amount, 2));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddRowParameters(MySqlCommand command, SalesDocumentSaveRow row)
    {
        command.Parameters.AddWithValue("@article", row.ArticleCode.Trim());
        command.Parameters.AddWithValue("@unit", row.Unit.Trim());
        command.Parameters.AddWithValue("@packages", row.Packages);
        command.Parameters.AddWithValue("@tare", decimal.Round(row.Tare, 3));
        command.Parameters.AddWithValue("@quantity", decimal.Round(row.Quantity, 3));
        command.Parameters.AddWithValue("@price", decimal.Round(row.Price, 3));
        command.Parameters.AddWithValue("@vat", decimal.Round(row.VatRate, 2));
        command.Parameters.AddWithValue("@amount", decimal.Round(row.Amount, 2));
    }
}
