using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class TrialBalanceRepository(MicronoteDb database)
{
    public async Task<TrialBalancePageModel> GetInitialAsync(
        int year,
        int baseYear,
        int? storeCode,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return new TrialBalancePageModel
        {
            Year = year,
            StoreCode = storeCode,
            Years = Years(baseYear, year),
            Stores = await ListStoresAsync(connection, cancellationToken)
        };
    }

    public async Task<TrialBalancePageModel> GetAsync(
        int year,
        int baseYear,
        int? storeCode,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var balances = await ListAccountBalancesAsync(connection, year, storeCode, cancellationToken);

        balances.TryGetValue(81, out var suppliersMaster);
        balances[81] = suppliersMaster - await InitialMasterBalanceAsync(connection, year, "F", cancellationToken);
        balances.TryGetValue(82, out var customersMaster);
        balances[82] = customersMaster + await InitialMasterBalanceAsync(connection, year, "C", cancellationToken);

        var accounts = await ListAccountsAsync(connection, cancellationToken);
        var equity = new List<TrialBalanceEquityRow>();
        var revenues = new List<(int Code, string Description, decimal Amount)>();
        var costs = new List<(int Code, string Description, decimal Amount)>();

        foreach (var account in accounts)
        {
            balances.TryGetValue(account.Code, out var balance);
            balance = Math.Round(balance, 2);
            if (balance == 0)
            {
                continue;
            }

            switch (account.Type)
            {
                case "P":
                    equity.Add(new TrialBalanceEquityRow(
                        account.Code, account.Description,
                        balance > 0 ? balance : 0,
                        balance < 0 ? Math.Abs(balance) : 0));
                    break;
                case "R":
                    revenues.Add((account.Code, account.Description, -balance));
                    break;
                case "C":
                    costs.Add((account.Code, account.Description, balance));
                    break;
            }
        }

        var totalAssets = equity.Sum(row => row.Assets);
        var totalLiabilities = -equity.Sum(row => row.Liabilities);
        var totalCosts = costs.Sum(row => row.Amount);
        var totalRevenues = Math.Abs(revenues.Sum(row => row.Amount));
        var income = revenues.Select(row => new TrialBalanceIncomeRow(
                row.Code, row.Description, 0, row.Amount,
                totalRevenues == 0 ? 0 : row.Amount * 100 / totalRevenues, "R"))
            .Concat(costs.Select(row => new TrialBalanceIncomeRow(
                row.Code, row.Description, row.Amount, 0,
                totalCosts == 0 ? 0 : row.Amount * 100 / totalCosts, "C")))
            .ToArray();

        return new TrialBalancePageModel
        {
            Year = year,
            StoreCode = storeCode,
            IsLoaded = true,
            Years = Years(baseYear, year),
            Stores = await ListStoresAsync(connection, cancellationToken),
            EquityRows = equity,
            IncomeRows = income,
            TotalAssets = Math.Round(totalAssets, 2),
            TotalLiabilities = Math.Round(totalLiabilities, 2),
            TotalCosts = Math.Round(totalCosts, 2),
            TotalRevenues = Math.Round(totalRevenues, 2)
        };
    }

    private static IReadOnlyList<int> Years(int baseYear, int selectedYear)
    {
        var years = Enumerable.Range(baseYear - 3, 4).Reverse().ToList();
        if (!years.Contains(selectedYear))
        {
            years.Add(selectedYear);
            years.Sort((left, right) => right.CompareTo(left));
        }
        return years;
    }

    private static async Task<IReadOnlyList<TrialBalanceStoreOption>> ListStoresAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT Codice, COALESCE(Nome, '') AS Nome FROM PuntiVendita ORDER BY Codice;";
        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<TrialBalanceStoreOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new TrialBalanceStoreOption(
                Convert.ToInt32(reader["Codice"]), Convert.ToString(reader["Nome"]) ?? ""));
        }
        return rows;
    }

    private static async Task<IReadOnlyList<(int Code, string Description, string Type)>> ListAccountsAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT Codice, COALESCE(Descrizione, '') AS Descrizione, COALESCE(Tipo, '') AS Tipo FROM Conti WHERE Tipo IN ('P','C','R') ORDER BY Tipo, Codice;";
        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<(int, string, string)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((Convert.ToInt32(reader["Codice"]), Convert.ToString(reader["Descrizione"]) ?? "", Convert.ToString(reader["Tipo"]) ?? ""));
        }
        return rows;
    }

    private static async Task<Dictionary<int, decimal>> ListAccountBalancesAsync(
        MySqlConnection connection,
        int year,
        int? storeCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT r.Conto,
                   COALESCE(SUM(CASE WHEN r.Segno = 'D' THEN r.Importo WHEN r.Segno = 'A' THEN -r.Importo ELSE 0 END), 0) AS Saldo
            FROM MovContRg r
            LEFT JOIN MovCont m ON m.ID = r.ID
            WHERE m.Anno = @year
              AND (@storeCode IS NULL OR m.PuntoV = @storeCode)
            GROUP BY r.Conto;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@storeCode", storeCode is null ? DBNull.Value : storeCode.Value);
        var rows = new Dictionary<int, decimal>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows[Convert.ToInt32(reader["Conto"])] = Money(reader["Saldo"]);
        }
        return rows;
    }

    private static async Task<decimal> InitialMasterBalanceAsync(
        MySqlConnection connection,
        int year,
        string type,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT COALESCE(SUM(Importo), 0) FROM SaldoIniCf WHERE Anno = @year AND CliFor = @type;";
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@type", type);
        return Money(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static decimal Money(object? value) =>
        value is null or DBNull ? 0 : Math.Round(Convert.ToDecimal(value), 2);
}
