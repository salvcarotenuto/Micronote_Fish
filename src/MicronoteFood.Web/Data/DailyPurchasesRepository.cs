using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class DailyPurchasesRepository(MicronoteDb database)
{
    public async Task<DateOnly?> LastDateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand("SELECT MAX(DataDoc) FROM Carico;", connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : DateOnly.FromDateTime(Convert.ToDateTime(value));
    }

    public async Task<DailyPurchasesPageModel> GetAsync(
        DateOnly date,
        string? selectedKey,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var purchases = await ListPurchasesAsync(connection, date, cancellationToken);
        var selected = purchases.FirstOrDefault(row => row.Key == selectedKey) ?? purchases.FirstOrDefault();

        return new DailyPurchasesPageModel
        {
            PurchaseDate = date,
            SelectedKey = selected?.Key ?? "",
            Purchases = purchases,
            Articles = await ListArticlesAsync(connection, date, cancellationToken),
            Details = selected is null
                ? []
                : await ListDetailsAsync(connection, selected, cancellationToken),
            Totals = await TotalsAsync(connection, date, cancellationToken)
        };
    }

    public async Task<SupplierBalanceModel?> GetSupplierBalanceAsync(
        int supplierCode,
        int exerciseYear,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        const string supplierSql = "SELECT COALESCE(Nome, '') AS Nome, COALESCE(SaldoIni, 0) AS SaldoIni FROM Fornitori WHERE Codice = @supplierCode LIMIT 1;";
        await using var supplierCommand = new MySqlCommand(supplierSql, connection);
        supplierCommand.Parameters.AddWithValue("@supplierCode", supplierCode);
        await using var supplierReader = await supplierCommand.ExecuteReaderAsync(cancellationToken);
        if (!await supplierReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var model = new SupplierBalanceModel
        {
            SupplierCode = supplierCode,
            SupplierName = Convert.ToString(supplierReader["Nome"]) ?? "",
            OpeningBalance = Decimal(supplierReader["SaldoIni"])
        };
        await supplierReader.DisposeAsync();

        const string movementSql = """
            SELECT DataMov, NumDoc, DataDoc, DecCausale, Acquisti, Pagamenti
            FROM (
                SELECT DataDoc AS DataMov,
                       '' AS NumDoc,
                       DataDoc,
                       'Docum. di acquisto' AS DecCausale,
                       COALESCE(Totale, 0) AS Acquisti,
                       0.0 AS Pagamenti,
                       COALESCE(Codice, 0) AS CodiceOrdine
                FROM Carico
                WHERE DataDoc BETWEEN @dateFrom AND @dateTo
                  AND Fornitore = @supplierCode
                UNION ALL
                SELECT mc.DataMov,
                       COALESCE(CAST(documento.NumDoc AS CHAR), '') AS NumDoc,
                       documento.DataDoc,
                       COALESCE(cc.Descrizione, '') AS DecCausale,
                       0.0 AS Acquisti,
                       COALESCE(mc.Importo, 0) AS Pagamenti,
                       COALESCE(mc.Codice, 0) AS CodiceOrdine
                FROM MovCassa mc
                LEFT JOIN CausaliCont cc ON cc.Codice = mc.Causale
                LEFT JOIN Carico documento ON documento.ID = mc.Documento
                WHERE mc.DataMov BETWEEN @dateFrom AND @dateTo
                  AND mc.CliFor = 'F'
                  AND mc.Ditta = @supplierCode
            ) movimenti
            ORDER BY DataMov, DecCausale, CodiceOrdine;
            """;
        await using var movementCommand = new MySqlCommand(movementSql, connection);
        movementCommand.Parameters.Add("@dateFrom", MySqlDbType.Date).Value = new DateOnly(2000, 1, 1).ToDateTime(TimeOnly.MinValue);
        movementCommand.Parameters.Add("@dateTo", MySqlDbType.Date).Value = new DateOnly(exerciseYear, 12, 31).ToDateTime(TimeOnly.MinValue);
        movementCommand.Parameters.AddWithValue("@supplierCode", supplierCode);
        var rows = new List<SupplierBalanceRow>();
        await using var movementReader = await movementCommand.ExecuteReaderAsync(cancellationToken);
        while (await movementReader.ReadAsync(cancellationToken))
        {
            var purchases = Decimal(movementReader["Acquisti"]);
            var payments = Decimal(movementReader["Pagamenti"]);
            model.TotalPurchases += purchases;
            model.TotalPayments += payments;
            rows.Add(new SupplierBalanceRow(
                DateOnly.FromDateTime(Convert.ToDateTime(movementReader["DataMov"])),
                Convert.ToString(movementReader["NumDoc"]) ?? "",
                Convert.ToString(movementReader["DecCausale"]) ?? "",
                purchases,
                payments));
        }
        model.Rows = rows;
        return model;
    }

    public async Task<IReadOnlyList<DailyPurchaseDetail>> GetDetailsAsync(
        int id,
        int year,
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return await ListDetailsAsync(connection, id, year, code, cancellationToken);
    }

    private static async Task<IReadOnlyList<DailyPurchaseItem>> ListPurchasesAsync(
        MySqlConnection connection,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(c.ID, 0) AS ID, c.Anno, c.Codice,
                   COALESCE(c.Fornitore, 0) AS Fornitore,
                   COALESCE(f.Nome, '') AS Nome,
                   COALESCE(c.Merce, 0) AS Merce,
                   COALESCE(c.Iva, 0) AS Iva,
                   COALESCE(c.Totale, 0) AS Totale,
                   0.0 AS Pagato,
                   COALESCE((
                       SELECT SUM(mc.Importo)
                       FROM MovCassa mc
                       WHERE mc.CliFor = 'F'
                         AND mc.Ditta = c.Fornitore
                         AND mc.Causale = 10
                         AND mc.DataMov = @date
                   ), 0) AS Cassa
            FROM Carico c
            LEFT JOIN Fornitori f ON f.Codice = c.Fornitore
            WHERE c.DataDoc = @date
            ORDER BY c.Codice;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@date", MySqlDbType.Date).Value = date.ToDateTime(TimeOnly.MinValue);
        var rows = new List<DailyPurchaseItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = Convert.ToInt32(reader["ID"]);
            var year = Convert.ToInt32(reader["Anno"]);
            var code = Convert.ToInt32(reader["Codice"]);
            rows.Add(new DailyPurchaseItem(
                id > 0 ? $"id:{id}" : $"legacy:{year}:{code}",
                id,
                year,
                code,
                Convert.ToInt32(reader["Fornitore"]),
                Convert.ToString(reader["Nome"]) ?? "",
                Decimal(reader["Merce"]),
                Decimal(reader["Iva"]),
                Decimal(reader["Totale"]),
                Decimal(reader["Pagato"]),
                Decimal(reader["Cassa"])));
        }
        return rows;
    }

    private static async Task<IReadOnlyList<DailyPurchasedArticle>> ListArticlesAsync(
        MySqlConnection connection,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(rg.Articolo, '') AS Articolo,
                   COALESCE(a.Descrizione, '') AS Descrizione,
                   COALESCE(SUM(rg.Quantita), 0) AS Quantita,
                   COALESCE(SUM(rg.Importo), 0) AS Importo
            FROM CaricoRg rg
            INNER JOIN Carico c ON c.Anno = rg.Anno AND c.Codice = rg.Codice
            LEFT JOIN Articoli a ON a.Codice = rg.Articolo
            WHERE c.DataDoc = @date
            GROUP BY rg.Articolo, a.Descrizione
            ORDER BY rg.Articolo;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@date", MySqlDbType.Date).Value = date.ToDateTime(TimeOnly.MinValue);
        var rows = new List<DailyPurchasedArticle>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new DailyPurchasedArticle(
                Convert.ToString(reader["Articolo"]) ?? "",
                Convert.ToString(reader["Descrizione"]) ?? "",
                Decimal(reader["Quantita"]),
                Decimal(reader["Importo"])));
        }
        return rows;
    }

    private static async Task<IReadOnlyList<DailyPurchaseDetail>> ListDetailsAsync(
        MySqlConnection connection,
        DailyPurchaseItem purchase,
        CancellationToken cancellationToken)
        => await ListDetailsAsync(connection, purchase.Id, purchase.Year, purchase.Code, cancellationToken);

    private static async Task<IReadOnlyList<DailyPurchaseDetail>> ListDetailsAsync(
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
                   COALESCE(rg.Colli, 0) AS Colli,
                   COALESCE(rg.Quantita, 0) AS Quantita,
                   COALESCE(rg.Prezzo, 0) AS Prezzo,
                   COALESCE(rg.AliqIva, 0) AS AliqIva,
                   COALESCE(rg.PrIvato, 0) AS PrIvato,
                   COALESCE(rg.Importo, 0) AS Importo
            FROM CaricoRg rg
            LEFT JOIN Articoli a ON a.Codice = rg.Articolo
            WHERE ((@id > 0 AND rg.ID = @id)
                OR (@id = 0 AND rg.Anno = @year AND rg.Codice = @code))
            ORDER BY rg.Riga;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@code", code);
        var rows = new List<DailyPurchaseDetail>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new DailyPurchaseDetail(
                Convert.ToInt32(reader["Riga"]),
                Convert.ToString(reader["Articolo"]) ?? "",
                Convert.ToString(reader["Descrizione"]) ?? "",
                Convert.ToString(reader["Ums"]) ?? "",
                Convert.ToInt32(reader["Colli"]),
                Decimal(reader["Quantita"]),
                Decimal(reader["Prezzo"]),
                Decimal(reader["AliqIva"]),
                Decimal(reader["PrIvato"]),
                Decimal(reader["Importo"])));
        }
        return rows;
    }

    private static async Task<DailyPurchaseTotals> TotalsAsync(
        MySqlConnection connection,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        const string purchaseSql = """
            SELECT COALESCE(SUM(Merce), 0) AS Merce,
                   COALESCE(SUM(Iva), 0) AS Iva,
                   COALESCE(SUM(Totale), 0) AS Totale,
                   0.0 AS Pagato
            FROM Carico WHERE DataDoc = @date;
            """;
        await using var command = new MySqlCommand(purchaseSql, connection);
        command.Parameters.Add("@date", MySqlDbType.Date).Value = date.ToDateTime(TimeOnly.MinValue);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var totals = new DailyPurchaseTotals
        {
            Goods = Decimal(reader["Merce"]),
            Vat = Decimal(reader["Iva"]),
            Purchases = Decimal(reader["Totale"]),
            Paid = Decimal(reader["Pagato"])
        };
        await reader.DisposeAsync();

        await using var cash = new MySqlCommand(
            "SELECT COALESCE(SUM(Importo), 0) FROM MovCassa WHERE CliFor = 'F' AND Causale = 10 AND DataMov = @date;",
            connection);
        cash.Parameters.Add("@date", MySqlDbType.Date).Value = date.ToDateTime(TimeOnly.MinValue);
        totals.CashOut = Decimal(await cash.ExecuteScalarAsync(cancellationToken));
        return totals;
    }

    private static decimal Decimal(object? value) => value is null or DBNull ? 0m : Convert.ToDecimal(value);
}
