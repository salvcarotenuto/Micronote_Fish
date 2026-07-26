using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class AccountingMovementSummaryRepository(MicronoteDb database)
{
    private static readonly int[] CollectionAccounts = [61, 62, 63, 65, 71];

    public Task<AccountingMovementSummaryPageModel> GetInitialAsync(int year, int baseYear) =>
        Task.FromResult(new AccountingMovementSummaryPageModel
        {
            Year = year,
            Years = Years(baseYear)
        });

    public async Task<AccountingMovementSummaryPageModel> GetAsync(
        int year,
        int baseYear,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var stores = await ListStoresAsync(connection, cancellationToken);
        var purchaseValues = await ListVatTotalsAsync(connection, "MovIva", year, "Settore = 10", cancellationToken);
        var revenueValues = await ListSalesVatTotalsAsync(connection, year, cancellationToken);

        var purchases = BuildVatRows(stores, purchaseValues, includeCommon: true);
        var revenues = BuildVatRows(stores, revenueValues, includeCommon: false);
        var costs = await ListCostsAsync(connection, year, cancellationToken);
        var collections = await ListCollectionsAsync(connection, year, cancellationToken);

        return new AccountingMovementSummaryPageModel
        {
            Year = year,
            IsLoaded = true,
            Years = Years(baseYear),
            Purchases = purchases,
            Costs = costs,
            Revenues = revenues,
            Collections = collections,
            TotalPurchases = purchases.LastOrDefault()?.Total ?? 0,
            TotalCosts = costs.LastOrDefault()?.Amount ?? 0,
            TotalRevenues = revenues.LastOrDefault()?.Total ?? 0,
            TotalCollections = collections.LastOrDefault()?.Amount ?? 0
        };
    }

    private static IReadOnlyList<int> Years(int baseYear) =>
        Enumerable.Range(baseYear - 3, 4).Reverse().ToArray();

    private static async Task<IReadOnlyList<(int Code, string Name)>> ListStoresAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT Codice, COALESCE(Nome, '') AS Nome FROM PuntiVendita ORDER BY Codice;";
        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<(int, string)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((Convert.ToInt32(reader["Codice"]), Convert.ToString(reader["Nome"]) ?? ""));
        }
        return rows;
    }

    private static async Task<IReadOnlyDictionary<int, VatTotals>> ListVatTotalsAsync(
        MySqlConnection connection,
        string table,
        int year,
        string condition,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT COALESCE(PuntoV, 0) AS PuntoV,
                   COALESCE(SUM(Imponibile), 0) AS Imponibile,
                   COALESCE(SUM(Iva), 0) AS Iva,
                   COALESCE(SUM(Totale), 0) AS Totale
            FROM {table}
            WHERE Anno = @year AND {condition}
            GROUP BY COALESCE(PuntoV, 0);
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        var rows = new Dictionary<int, VatTotals>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows[Convert.ToInt32(reader["PuntoV"])] = new VatTotals(
                Decimal(reader["Imponibile"]), Decimal(reader["Iva"]), Decimal(reader["Totale"]));
        }
        return rows;
    }

    private static async Task<IReadOnlyDictionary<int, VatTotals>> ListSalesVatTotalsAsync(
        MySqlConnection connection,
        int year,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(PuntoV, 0) AS PuntoV,
                   COALESCE(SUM(Imponibile + NonImpo), 0) AS Imponibile,
                   COALESCE(SUM(Iva), 0) AS Iva,
                   COALESCE(SUM(Imponibile + NonImpo + Iva), 0) AS Totale
            FROM MovivaRg
            WHERE Anno = @year AND Settore = 20
            GROUP BY COALESCE(PuntoV, 0);
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        var rows = new Dictionary<int, VatTotals>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows[Convert.ToInt32(reader["PuntoV"])] = new VatTotals(
                Decimal(reader["Imponibile"]), Decimal(reader["Iva"]), Decimal(reader["Totale"]));
        }

        return rows;
    }

    private static IReadOnlyList<AccountingMovementVatSummaryRow> BuildVatRows(
        IReadOnlyList<(int Code, string Name)> stores,
        IReadOnlyDictionary<int, VatTotals> values,
        bool includeCommon)
    {
        var source = stores.Select(store =>
        {
            values.TryGetValue(store.Code, out var totals);
            return (Code: (int?)store.Code, Description: store.Name, Totals: totals ?? new VatTotals());
        }).ToList();
        if (includeCommon)
        {
            values.TryGetValue(0, out var common);
            source.Add((null, "Spese comuni", common ?? new VatTotals()));
        }

        var total = source.Sum(row => row.Totals.Total);
        var rows = source.Select(row => new AccountingMovementVatSummaryRow(
            row.Code, row.Description, row.Totals.Taxable, row.Totals.Vat, row.Totals.Total,
            Percent(row.Totals.Total, total))).ToList();
        rows.Add(new AccountingMovementVatSummaryRow(
            null, "Totale", source.Sum(row => row.Totals.Taxable), source.Sum(row => row.Totals.Vat),
            total, Percent(total, total), true));
        return rows;
    }

    private static async Task<IReadOnlyList<AccountingMovementAmountSummaryRow>> ListCostsAsync(
        MySqlConnection connection,
        int year,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c.Codice, COALESCE(c.Descrizione, '') AS Descrizione,
                   COALESCE(SUM(CASE WHEN r.Segno = 'D' THEN r.Importo WHEN r.Segno = 'A' THEN -r.Importo ELSE 0 END), 0) AS Importo
            FROM Conti c
            LEFT JOIN MovContRg r ON r.Conto = c.Codice AND r.Anno = @year
            WHERE c.Tipo = 'C'
            GROUP BY c.Codice, c.Descrizione
            HAVING Importo <> 0
            ORDER BY c.Codice;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        var raw = new List<(int Code, string Description, decimal Amount)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                raw.Add((Convert.ToInt32(reader["Codice"]), Convert.ToString(reader["Descrizione"]) ?? "", Decimal(reader["Importo"])));
            }
        }
        var total = raw.Sum(row => row.Amount);
        var rows = raw.Select(row => new AccountingMovementAmountSummaryRow(
            row.Code, row.Description, row.Amount, Percent(row.Amount, total))).ToList();
        rows.Add(new AccountingMovementAmountSummaryRow(null, "Totale", total, Percent(total, total), true));
        return rows;
    }

    private static async Task<IReadOnlyList<AccountingMovementAmountSummaryRow>> ListCollectionsAsync(
        MySqlConnection connection,
        int year,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c.Codice, COALESCE(c.Descrizione, '') AS Descrizione,
                   COALESCE(SUM(CASE WHEN r.Settore = 20 AND r.Segno = 'D' AND r.Anno = @year THEN r.Importo ELSE 0 END), 0) AS Importo
            FROM Conti c
            LEFT JOIN MovContRg r ON r.Conto = c.Codice
            WHERE c.Codice IN (61, 62, 63, 65, 71)
            GROUP BY c.Codice, c.Descrizione
            ORDER BY FIELD(c.Codice, 61, 62, 63, 65, 71);
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        var amounts = new Dictionary<int, (string Description, decimal Amount)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                amounts[Convert.ToInt32(reader["Codice"])] = (Convert.ToString(reader["Descrizione"]) ?? "", Decimal(reader["Importo"]));
            }
        }
        var total = amounts.Values.Sum(row => row.Amount);
        var rows = CollectionAccounts.Select(code =>
        {
            amounts.TryGetValue(code, out var value);
            return new AccountingMovementAmountSummaryRow(code, value.Description ?? "", value.Amount, Percent(value.Amount, total));
        }).ToList();
        rows.Add(new AccountingMovementAmountSummaryRow(null, "Totale", total, Percent(total, total), true));
        return rows;
    }

    private static decimal Percent(decimal value, decimal total) => total == 0 ? 0 : value * 100 / total;
    private static decimal Decimal(object? value) => value is null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
    private sealed record VatTotals(decimal Taxable = 0, decimal Vat = 0, decimal Total = 0);
}
