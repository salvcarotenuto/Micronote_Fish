using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class DailySalesRepository(MicronoteDb database)
{
    public async Task<DateOnly?> LastDateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand("SELECT MAX(DataDoc) FROM Vendite;", connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : DateOnly.FromDateTime(Convert.ToDateTime(value));
    }

    public async Task<DailySalesPageModel> GetAsync(DateOnly date, int storeCode, string? selectedKey, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var sales = await ListSalesAsync(connection, date, storeCode, cancellationToken);
        var selected = sales.FirstOrDefault(row => row.Key == selectedKey) ?? sales.FirstOrDefault();
        return new DailySalesPageModel
        {
            SaleDate = date,
            StoreCode = storeCode,
            SelectedKey = selected?.Key ?? "",
            Stores = await ListStoresAsync(connection, cancellationToken),
            Sales = sales,
            Articles = await ListArticlesAsync(connection, date, storeCode, cancellationToken),
            Details = selected is null ? [] : await ListDetailsAsync(connection, selected.Id, selected.Year, selected.Code, cancellationToken),
            Totals = await TotalsAsync(connection, date, storeCode, cancellationToken)
        };
    }

    public async Task<IReadOnlyList<DailySaleDetail>> GetDetailsAsync(int id, int year, int code, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return await ListDetailsAsync(connection, id, year, code, cancellationToken);
    }

    private static async Task<IReadOnlyList<DailySaleItem>> ListSalesAsync(MySqlConnection connection, DateOnly date, int storeCode, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(v.ID, 0) AS ID, v.Anno, v.Codice, COALESCE(v.Cliente, 0) AS Cliente,
                   COALESCE(c.Nome, '') AS Nome, COALESCE(v.Merce, 0) AS Merce, COALESCE(v.Iva, 0) AS Iva,
                   COALESCE(v.Totale, 0) AS Totale, COALESCE(v.Pagato, 0) AS Pagato,
                   COALESCE((SELECT SUM(mc.Importo) FROM MovCassa mc
                       WHERE mc.CliFor = 'C' AND mc.Ditta = v.Cliente AND mc.Causale = 20 AND mc.DataMov = @date), 0) AS Cassa
            FROM Vendite v LEFT JOIN Clienti c ON c.Codice = v.Cliente
            WHERE v.DataDoc = @date AND (@storeCode = 0 OR v.PuntoV = @storeCode)
            ORDER BY v.Codice;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@date", MySqlDbType.Date).Value = date.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@storeCode", storeCode);
        var rows = new List<DailySaleItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = Convert.ToInt32(reader["ID"]); var year = Convert.ToInt32(reader["Anno"]); var code = Convert.ToInt32(reader["Codice"]);
            rows.Add(new(id > 0 ? $"id:{id}" : $"legacy:{year}:{code}", id, year, code, Convert.ToInt32(reader["Cliente"]), Convert.ToString(reader["Nome"]) ?? "", Decimal(reader["Merce"]), Decimal(reader["Iva"]), Decimal(reader["Totale"]), Decimal(reader["Pagato"]), Decimal(reader["Cassa"])));
        }
        return rows;
    }

    private static async Task<IReadOnlyList<DailySoldArticle>> ListArticlesAsync(MySqlConnection connection, DateOnly date, int storeCode, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(rg.Articolo, '') AS Articolo, COALESCE(a.Descrizione, '') AS Descrizione,
                   COALESCE(SUM(rg.Quantita), 0) AS Quantita, COALESCE(SUM(rg.Importo), 0) AS Importo
            FROM VenditeRg rg INNER JOIN Vendite v ON v.ID = rg.ID LEFT JOIN Articoli a ON a.Codice = rg.Articolo
            WHERE v.DataDoc = @date AND (@storeCode = 0 OR v.PuntoV = @storeCode)
            GROUP BY rg.Articolo, a.Descrizione ORDER BY rg.Articolo;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@date", MySqlDbType.Date).Value = date.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@storeCode", storeCode);
        var rows = new List<DailySoldArticle>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) rows.Add(new(Convert.ToString(reader["Articolo"]) ?? "", Convert.ToString(reader["Descrizione"]) ?? "", Decimal(reader["Quantita"]), Decimal(reader["Importo"])));
        return rows;
    }

    private static async Task<IReadOnlyList<DailySaleDetail>> ListDetailsAsync(MySqlConnection connection, int id, int year, int code, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(rg.Riga, 0) AS Riga, COALESCE(rg.Articolo, '') AS Articolo, COALESCE(a.Descrizione, '') AS Descrizione,
                   COALESCE(rg.Ums, '') AS Ums, COALESCE(rg.Colli, 0) AS Colli, COALESCE(rg.Quantita, 0) AS Quantita,
                   COALESCE(rg.Prezzo, 0) AS Prezzo, COALESCE(rg.AliqIva, 0) AS AliqIva,
                   ROUND(COALESCE(rg.Prezzo, 0) * (1 + COALESCE(rg.AliqIva, 0) / 100), 2) AS PrIvato,
                   COALESCE(rg.Importo, 0) AS Importo
            FROM VenditeRg rg LEFT JOIN Articoli a ON a.Codice = rg.Articolo
            WHERE rg.ID = @id
            ORDER BY rg.Riga;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        var rows = new List<DailySaleDetail>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) rows.Add(new(Convert.ToInt32(reader["Riga"]), Convert.ToString(reader["Articolo"]) ?? "", Convert.ToString(reader["Descrizione"]) ?? "", Convert.ToString(reader["Ums"]) ?? "", Convert.ToInt32(reader["Colli"]), Decimal(reader["Quantita"]), Decimal(reader["Prezzo"]), Decimal(reader["AliqIva"]), Decimal(reader["PrIvato"]), Decimal(reader["Importo"])));
        return rows;
    }

    private static async Task<DailySaleTotals> TotalsAsync(MySqlConnection connection, DateOnly date, int storeCode, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(SUM(Merce), 0) AS Merce, COALESCE(SUM(Iva), 0) AS Iva, COALESCE(SUM(Totale), 0) AS Totale, COALESCE(SUM(Pagato), 0) AS Pagato
            FROM Vendite WHERE DataDoc = @date AND (@storeCode = 0 OR PuntoV = @storeCode);
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@date", MySqlDbType.Date).Value = date.ToDateTime(TimeOnly.MinValue); command.Parameters.AddWithValue("@storeCode", storeCode);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); await reader.ReadAsync(cancellationToken);
        var totals = new DailySaleTotals { Goods = Decimal(reader["Merce"]), Vat = Decimal(reader["Iva"]), Sales = Decimal(reader["Totale"]), Paid = Decimal(reader["Pagato"]) };
        await reader.DisposeAsync();
        await using var cash = new MySqlCommand("SELECT COALESCE(SUM(Importo), 0) FROM MovCassa WHERE CliFor = 'C' AND Causale = 20 AND DataMov = @date;", connection);
        cash.Parameters.Add("@date", MySqlDbType.Date).Value = date.ToDateTime(TimeOnly.MinValue);
        totals.CashIn = Decimal(await cash.ExecuteScalarAsync(cancellationToken)); return totals;
    }

    private static async Task<IReadOnlyList<DailySaleStore>> ListStoresAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand("SELECT Codice, COALESCE(Nome, '') AS Nome FROM PuntiVendita WHERE COALESCE(Attivo, 1) <> 0 ORDER BY Codice;", connection);
        var rows = new List<DailySaleStore>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) rows.Add(new(Convert.ToInt32(reader["Codice"]), Convert.ToString(reader["Nome"]) ?? "")); return rows;
    }
    private static decimal Decimal(object? value) => value is null or DBNull ? 0m : Convert.ToDecimal(value);
}
