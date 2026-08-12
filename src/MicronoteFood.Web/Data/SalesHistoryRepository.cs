using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class SalesHistoryRepository(MicronoteDb database)
{
    public async Task<SalesHistoryPageModel> GetAsync(
        int year,
        int month,
        int customerCode,
        int storeCode,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var years = await ListYearsAsync(connection, year, cancellationToken);
        var stores = await ListStoresAsync(connection, cancellationToken);
        var customerName = customerCode > 0
            ? await LoadCustomerNameAsync(connection, customerCode, cancellationToken)
            : "";

        return new SalesHistoryPageModel
        {
            Year = year,
            Month = Math.Clamp(month, 0, 12),
            CustomerCode = Math.Max(customerCode, 0),
            CustomerName = customerName,
            StoreCode = Math.Max(storeCode, 0),
            Years = years,
            Stores = stores,
            Sales = [],
            Totals = SalesHistoryTotals.Empty
        };
    }

    public async Task<IReadOnlyList<SalesHistoryListItem>> ListPageAsync(int year, int month, int customerCode, int storeCode, int offset, int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return await ListSalesAsync(connection, year, Math.Clamp(month, 0, 12), Math.Max(customerCode, 0), Math.Max(storeCode, 0), Math.Max(offset, 0), Math.Clamp(limit, 1, 250), cancellationToken);
    }

    private static async Task<IReadOnlyList<SalesEntryStoreRow>> ListStoresAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Codice, COALESCE(Nome, '') AS Nome
            FROM PuntiVendita
            ORDER BY Nome, Codice;
            """;
        var stores = new List<SalesEntryStoreRow>();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            stores.Add(new(
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Nome"]) ?? ""));
        return stores;
    }

    private static async Task<string> LoadCustomerNameAsync(
        MySqlConnection connection,
        int customerCode,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COALESCE(Nome, '') FROM Clienti WHERE Codice = @code LIMIT 1;",
            connection);
        command.Parameters.AddWithValue("@code", customerCode);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? "";
    }

    private static async Task<IReadOnlyList<int>> ListYearsAsync(
        MySqlConnection connection,
        int currentYear,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT Anno
            FROM Vendite
            ORDER BY Anno DESC;
            """;
        var years = new List<int>();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            years.Add(Convert.ToInt32(reader["Anno"]));
        if (!years.Contains(currentYear))
            years.Insert(0, currentYear);
        return years;
    }

    private static async Task<IReadOnlyList<SalesHistoryListItem>> ListSalesAsync(
        MySqlConnection connection,
        int year,
        int month,
        int customerCode,
        int storeCode, int offset, int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT v.ID, v.Anno, v.Codice, v.NumDoc, v.DataDoc,
                   v.Cliente, COALESCE(c.Nome, '') AS ClienteNome,
                   COALESCE(v.Merce, 0) AS Merce,
                   COALESCE(v.Iva, 0) AS Iva,
                   COALESCE(v.Totale, 0) AS Totale,
                   COALESCE(v.Abbuono, 0) AS Abbuono,
                   COALESCE(v.PuntoV, 0) AS PuntoV
            FROM Vendite v
            LEFT JOIN Clienti c ON c.Codice = v.Cliente
            WHERE v.Anno = @year
              AND (@month = 0 OR MONTH(v.DataDoc) = @month)
              AND (@customer = 0 OR v.Cliente = @customer)
              AND (@store = 0 OR v.PuntoV = @store)
            ORDER BY v.DataDoc DESC, v.NumDoc DESC, v.Codice DESC
            LIMIT @limit OFFSET @offset;
            """;
        var rows = new List<SalesHistoryListItem>();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@month", month);
        command.Parameters.AddWithValue("@customer", customerCode);
        command.Parameters.AddWithValue("@store", storeCode);
        command.Parameters.AddWithValue("@offset", offset);
        command.Parameters.AddWithValue("@limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new SalesHistoryListItem(
                Convert.ToInt32(reader["ID"]),
                Convert.ToInt32(reader["Anno"]),
                Convert.ToInt32(reader["Codice"]),
                Convert.ToInt32(reader["NumDoc"]),
                reader["DataDoc"] is DBNull
                    ? null
                    : DateOnly.FromDateTime(Convert.ToDateTime(reader["DataDoc"])),
                Convert.ToInt32(reader["Cliente"]),
                Convert.ToString(reader["ClienteNome"]) ?? "",
                Money(reader["Merce"]),
                Money(reader["Iva"]),
                Money(reader["Totale"]),
                Money(reader["Abbuono"]),
                Convert.ToInt32(reader["PuntoV"])));
        }
        return rows;
    }

    public async Task<IReadOnlyList<SalesHistoryDetailItem>> ListDetailsAsync(
        int saleId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT vr.Riga, vr.Articolo, COALESCE(a.Descrizione, '') AS Descrizione,
                   COALESCE(vr.Ums, '') AS Ums, COALESCE(vr.Colli, 0) AS Colli,
                   COALESCE(vr.Tara, 0) AS Tara,
                   COALESCE(vr.Quantita, 0) AS Quantita,
                   COALESCE(vr.Prezzo, 0) AS Prezzo,
                   COALESCE(vr.AliqIva, 0) AS Iva,
                   ROUND(COALESCE(vr.Prezzo, 0) * (1 + COALESCE(vr.AliqIva, 0) / 100), 2) AS PrezzoIvato,
                   COALESCE(vr.Importo, 0) AS Importo
            FROM VenditeRg vr
            LEFT JOIN Articoli a ON a.Codice = vr.Articolo
            WHERE vr.ID = @saleId
            ORDER BY vr.Riga;
            """;
        var rows = new List<SalesHistoryDetailItem>();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@saleId", saleId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new SalesHistoryDetailItem(
                Convert.ToInt32(reader["Riga"]),
                Convert.ToString(reader["Articolo"]) ?? "",
                Convert.ToString(reader["Descrizione"]) ?? "",
                Convert.ToString(reader["Ums"]) ?? "",
                Convert.ToInt32(reader["Colli"]),
                Quantity(reader["Tara"]),
                Quantity(reader["Quantita"]),
                Quantity(reader["Prezzo"]),
                Money(reader["Iva"]),
                Money(reader["PrezzoIvato"]),
                Money(reader["Importo"])));
        }
        return rows;
    }

    private static SalesHistoryTotals TotalsFrom(IReadOnlyList<SalesHistoryListItem> rows)
    {
        return new(
            rows.Sum(row => row.Merchandise),
            rows.Sum(row => row.Vat),
            rows.Sum(row => row.Total),
            rows.Sum(row => row.Discount));
    }

    private static decimal Money(object value) =>
        value is null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);

    private static decimal Quantity(object value) =>
        value is null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
}
