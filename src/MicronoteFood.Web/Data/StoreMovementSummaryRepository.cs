using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class StoreMovementSummaryRepository(MicronoteDb database)
{
    public async Task<StoreMovementSummaryPageModel> GetInitialAsync(
        int year,
        int baseYear,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return new StoreMovementSummaryPageModel
        {
            Year = year,
            Years = Years(baseYear),
            Stores = await ListStoresAsync(connection, cancellationToken)
        };
    }

    public async Task<StoreMovementSummaryPageModel> GetAsync(
        int year,
        int baseYear,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var stores = await ListStoresAsync(connection, cancellationToken);
        var revenueContext = await LoadRevenueContextAsync(connection, year, stores, cancellationToken);

        return new StoreMovementSummaryPageModel
        {
            Year = year,
            IsLoaded = true,
            Years = Years(baseYear),
            Stores = stores,
            Revenues = new StoreMovementSummaryGrid(
                "Ricavi del periodo",
                BuildRevenueRows(revenueContext, stores)),
            Costs = new StoreMovementSummaryGrid(
                "Costi del periodo",
                await BuildCostRowsAsync(connection, year, stores, revenueContext.TotalRevenue, cancellationToken))
        };
    }

    private static IReadOnlyList<int> Years(int selectedYear)
    {
        return Enumerable.Range(selectedYear - 3, 4).Reverse().ToArray();
    }

    private static async Task<IReadOnlyList<StoreMovementSummaryStore>> ListStoresAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Codice, COALESCE(Nome, '') AS Nome
            FROM PuntiVendita
            ORDER BY Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var stores = new List<StoreMovementSummaryStore>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            stores.Add(new StoreMovementSummaryStore(
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Nome"]) ?? ""));
        }

        return stores;
    }

    private static async Task<RevenueContext> LoadRevenueContextAsync(
        MySqlConnection connection,
        int year,
        IReadOnlyList<StoreMovementSummaryStore> stores,
        CancellationToken cancellationToken)
    {
        var invoiceSales = await ScalarDecimalAsync(
            connection,
            """
            SELECT COALESCE(SUM(Imponibile), 0)
            FROM MovIva
            WHERE Settore = 30 AND Anno = @year;
            """,
            year,
            cancellationToken);

        var totals = await ReadRevenueTotalsAsync(connection, year, cancellationToken);
        var rowTotals = new decimal[]
        {
            invoiceSales,
            totals.TaxableSales,
            totals.NonTaxableSales,
            invoiceSales + totals.Imponibile,
            0,
            totals.Cash,
            totals.Card,
            totals.Tickets,
            totals.Checks,
            totals.Other,
            totals.Pending,
            totals.Losses
        };

        var storeRows = new Dictionary<int, decimal[]>();
        foreach (var store in stores)
        {
            var storeTotals = await ReadStoreRevenueTotalsAsync(connection, year, store.Code, cancellationToken);
            storeRows[store.Code] =
            [
                0,
                storeTotals.TaxableSales,
                storeTotals.NonTaxableSales,
                storeTotals.Imponibile,
                storeTotals.Imponibile,
                storeTotals.Cash,
                storeTotals.Card,
                storeTotals.Tickets,
                storeTotals.Checks,
                storeTotals.Other,
                storeTotals.Pending,
                storeTotals.Losses
            ];
        }

        var totalRevenue = invoiceSales + totals.Imponibile;
        var totalCollections = totals.Cash +
            totals.Card +
            totals.Tickets +
            totals.Checks +
            totals.Other +
            totals.Pending +
            totals.Losses;

        return new RevenueContext(
            rowTotals,
            storeRows,
            totalRevenue,
            totalRevenue + 0.01m,
            totalCollections + 0.01m);
    }

    private static IReadOnlyList<StoreMovementSummaryRow> BuildRevenueRows(
        RevenueContext context,
        IReadOnlyList<StoreMovementSummaryStore> stores)
    {
        string[] descriptions =
        [
            "Vendite con fattura",
            "Vendite per corrispettivi",
            "Altri ricavi di vendita",
            "Totale ricavi",
            "",
            "Contanti",
            "Carta",
            "Tickets",
            "Assegni",
            "Cassa transitoria",
            "Sospesi",
            "Ammanchi"
        ];

        var rows = new List<StoreMovementSummaryRow>();
        for (var index = 0; index < descriptions.Length; index++)
        {
            if (index == 4)
            {
                rows.Add(new StoreMovementSummaryRow(null, "", 0, 0, [], IsSeparator: true));
                continue;
            }

            var total = context.RowTotals[index];
            var share = index switch
            {
                0 or 1 or 2 => Percent(total, context.RevenueQuotaBase),
                >= 5 and <= 11 => Percent(total, context.CollectionQuotaBase),
                _ => 0
            };

            var values = stores.Select(store =>
            {
                var amount = context.StoreRows.TryGetValue(store.Code, out var storeRows) && index < storeRows.Length
                    ? storeRows[index]
                    : 0;

                var baseTotal = index switch
                {
                    1 => context.RowTotals[1],
                    2 => context.RowTotals[2],
                    3 or 4 => context.RevenueQuotaBase,
                    >= 5 and <= 11 => context.RowTotals[index],
                    _ => 0
                };

                return new StoreMovementSummaryStoreValue(amount, 0, Percent(amount, baseTotal));
            }).ToArray();

            rows.Add(new StoreMovementSummaryRow(
                null,
                descriptions[index],
                total,
                share,
                values,
                IsTotal: index == 3));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<StoreMovementSummaryRow>> BuildCostRowsAsync(
        MySqlConnection connection,
        int year,
        IReadOnlyList<StoreMovementSummaryStore> stores,
        decimal totalRevenue,
        CancellationToken cancellationToken)
    {
        var accounts = await ListCostAccountsAsync(connection, year, cancellationToken);
        var accountRows = new List<(int AccountCode, string Description, decimal Total)>();
        foreach (var account in accounts)
        {
            var total = await AccountMovementAmountAsync(connection, year, account.Code, null, cancellationToken);
            if (total != 0)
            {
                accountRows.Add((account.Code, account.Description, total));
            }
        }

        var totalCosts = accountRows.Sum(row => row.Total);
        var rows = new List<StoreMovementSummaryRow>
        {
            new(null, "Totale costi", totalCosts, 0, [], IsTotal: true),
            new(null, "", 0, 0, [], IsSeparator: true)
        };

        foreach (var account in accountRows)
        {
            var storeValues = new List<StoreMovementSummaryStoreValue>();
            foreach (var store in stores)
            {
                var amount = await AccountMovementAmountAsync(connection, year, account.AccountCode, store.Code, cancellationToken);
                storeValues.Add(new StoreMovementSummaryStoreValue(
                    amount,
                    Percent(amount, account.Total),
                    Percent(amount, totalRevenue + 0.01m)));
            }

            var commonAmount = await AccountCommonAmountAsync(connection, year, account.AccountCode, cancellationToken);
            rows.Add(new StoreMovementSummaryRow(
                account.AccountCode,
                account.Description,
                account.Total,
                Percent(account.Total, totalCosts),
                storeValues,
                new StoreMovementSummaryStoreValue(
                    commonAmount,
                    Percent(commonAmount, account.Total),
                    Percent(commonAmount, totalRevenue + 0.01m))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<(int Code, string Description)>> ListCostAccountsAsync(
        MySqlConnection connection,
        int year,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT r.Conto, COALESCE(c.Descrizione, '') AS Descrizione
            FROM MovContRg r
            LEFT JOIN Conti c ON c.Codice = r.Conto
            WHERE c.Tipo = 'C' AND r.Anno = @year
            GROUP BY r.Conto, c.Descrizione
            ORDER BY r.Conto;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);

        var rows = new List<(int Code, string Description)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((
                Convert.ToInt32(reader["Conto"]),
                Convert.ToString(reader["Descrizione"]) ?? ""));
        }

        return rows;
    }

    private static async Task<RevenueTotals> ReadRevenueTotalsAsync(
        MySqlConnection connection,
        int year,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT fiscal.Imponibile, fiscal.NonImpo,
                   receipts.Altro, receipts.Contanti, receipts.Carta,
                   receipts.Tickets, receipts.Assegni, receipts.Sospesi, receipts.Perdite
            FROM (
                SELECT COALESCE(SUM(Imponibile), 0) AS Imponibile,
                       COALESCE(SUM(NonImpo), 0) AS NonImpo
                FROM MovivaRg
                WHERE Anno = @year AND Settore = 20
            ) fiscal
            CROSS JOIN (
                SELECT COALESCE(SUM(Altro), 0) AS Altro,
                       COALESCE(SUM(Contanti), 0) AS Contanti,
                       COALESCE(SUM(Carta), 0) AS Carta,
                       COALESCE(SUM(Tickets), 0) AS Tickets,
                       COALESCE(SUM(Assegni), 0) AS Assegni,
                       COALESCE(SUM(Sospesi), 0) AS Sospesi,
                       COALESCE(SUM(Perdite), 0) AS Perdite
                FROM VenditeRg
                WHERE Anno = @year
            ) receipts;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new RevenueTotals();
        }

        var taxable = Decimal(reader["Imponibile"]);
        var nonTaxable = Decimal(reader["NonImpo"]);
        var other = Decimal(reader["Altro"]);
        return new RevenueTotals(
            taxable + nonTaxable,
            nonTaxable,
            taxable,
            Decimal(reader["Contanti"]),
            Decimal(reader["Carta"]),
            Decimal(reader["Tickets"]),
            Decimal(reader["Assegni"]),
            other,
            Decimal(reader["Sospesi"]),
            Decimal(reader["Perdite"]));
    }

    private static async Task<RevenueTotals> ReadStoreRevenueTotalsAsync(
        MySqlConnection connection,
        int year,
        int storeCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT fiscal.Imponibile, fiscal.NonImpo,
                   receipts.Altro, receipts.Contanti, receipts.Carta,
                   receipts.Tickets, receipts.Assegni, receipts.Sospesi, receipts.Perdite
            FROM (
                SELECT COALESCE(SUM(Imponibile), 0) AS Imponibile,
                       COALESCE(SUM(NonImpo), 0) AS NonImpo
                FROM MovivaRg
                WHERE Anno = @year AND Settore = 20 AND PuntoV = @storeCode
            ) fiscal
            CROSS JOIN (
                SELECT COALESCE(SUM(Altro), 0) AS Altro,
                       COALESCE(SUM(Contanti), 0) AS Contanti,
                       COALESCE(SUM(Carta), 0) AS Carta,
                       COALESCE(SUM(Tickets), 0) AS Tickets,
                       COALESCE(SUM(Assegni), 0) AS Assegni,
                       COALESCE(SUM(Sospesi), 0) AS Sospesi,
                       COALESCE(SUM(Perdite), 0) AS Perdite
                FROM VenditeRg
                WHERE Anno = @year AND PuntoV = @storeCode
            ) receipts;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@storeCode", storeCode);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new RevenueTotals();
        }

        var taxable = Decimal(reader["Imponibile"]);
        var nonTaxable = Decimal(reader["NonImpo"]);
        var other = Decimal(reader["Altro"]);
        return new RevenueTotals(
            taxable + nonTaxable,
            nonTaxable,
            taxable,
            Decimal(reader["Contanti"]),
            Decimal(reader["Carta"]),
            Decimal(reader["Tickets"]),
            Decimal(reader["Assegni"]),
            other,
            Decimal(reader["Sospesi"]),
            Decimal(reader["Perdite"]));
    }

    private static async Task<decimal> AccountMovementAmountAsync(
        MySqlConnection connection,
        int year,
        int accountCode,
        int? storeCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(SUM(CASE WHEN r.Segno = 'D' THEN r.Importo ELSE -r.Importo END), 0)
            FROM MovContRg r
            LEFT JOIN MovCont m ON m.ID = r.ID
            WHERE m.Anno = @year
              AND r.Conto = @accountCode
              AND (@storeCode IS NULL OR m.PuntoV = @storeCode);
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@accountCode", accountCode);
        command.Parameters.AddWithValue("@storeCode", storeCode is null ? DBNull.Value : storeCode.Value);
        return Decimal(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<decimal> AccountCommonAmountAsync(
        MySqlConnection connection,
        int year,
        int accountCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(SUM(CASE WHEN r.Segno = 'D' THEN r.Importo ELSE -r.Importo END), 0)
            FROM MovContRg r
            LEFT JOIN MovCont m ON m.ID = r.ID
            WHERE m.Anno = @year
              AND r.Conto = @accountCode
              AND COALESCE(m.PuntoV, 0) = 0
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@accountCode", accountCode);
        return Decimal(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<decimal> ScalarDecimalAsync(
        MySqlConnection connection,
        string sql,
        int year,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        return Decimal(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static decimal Percent(decimal value, decimal total) =>
        total == 0 ? 0 : value * 100 / total;

    private static decimal Decimal(object? value) =>
        value is null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);

    private sealed record RevenueContext(
        decimal[] RowTotals,
        IReadOnlyDictionary<int, decimal[]> StoreRows,
        decimal TotalRevenue,
        decimal RevenueQuotaBase,
        decimal CollectionQuotaBase);

    private sealed record RevenueTotals(
        decimal Imponibile = 0,
        decimal NonTaxableSales = 0,
        decimal TaxableSales = 0,
        decimal Cash = 0,
        decimal Card = 0,
        decimal Tickets = 0,
        decimal Checks = 0,
        decimal Other = 0,
        decimal Pending = 0,
        decimal Losses = 0);
}
