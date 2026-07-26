using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class CustomerSupplierBalanceSummaryRepository(MicronoteDb database)
{
    public async Task<CustomerSupplierBalanceSummaryPageModel> GetAsync(
        int year,
        int baseYear,
        bool showZero,
        int? storeCode,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return new CustomerSupplierBalanceSummaryPageModel
        {
            Year = year,
            ShowZero = showZero,
            StoreCode = storeCode,
            Years = Years(baseYear, year),
            Stores = await ListStoresAsync(connection, cancellationToken),
            Customers = await ListBalancesAsync(connection, "C", "Clienti", year, showZero, storeCode, cancellationToken),
            Suppliers = await ListBalancesAsync(connection, "F", "Fornitori", year, showZero, storeCode, cancellationToken)
        };
    }

    private static IReadOnlyList<int> Years(int baseYear, int selectedYear)
    {
        var years = Enumerable.Range(baseYear - 2, 3).Reverse().ToList();
        if (!years.Contains(selectedYear))
        {
            years.Add(selectedYear);
            years.Sort((left, right) => right.CompareTo(left));
        }
        return years;
    }

    private static async Task<IReadOnlyList<CustomerSupplierBalanceStoreOption>> ListStoresAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT Codice, COALESCE(Nome, '') AS Nome FROM PuntiVendita ORDER BY Codice;";
        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<CustomerSupplierBalanceStoreOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CustomerSupplierBalanceStoreOption(
                Convert.ToInt32(reader["Codice"]), Convert.ToString(reader["Nome"]) ?? ""));
        }
        return rows;
    }

    private static async Task<IReadOnlyList<CustomerSupplierBalanceSummaryRow>> ListBalancesAsync(
        MySqlConnection connection,
        string type,
        string partyTable,
        int year,
        bool showZero,
        int? storeCode,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT a.Codice,
                   COALESCE(a.Nome, '') AS Nome,
                   COALESCE(s.Importo, 0) AS SaldoIniziale,
                   COALESCE(m.Dare, 0) AS Dare,
                   COALESCE(m.Avere, 0) AS Avere
            FROM {partyTable} a
            LEFT JOIN SaldoIniCf s
              ON s.Anno = @year AND s.CliFor = @type AND s.Ditta = a.Codice
            LEFT JOIN (
                SELECT mc.Ditta,
                       SUM(CASE WHEN cc.Segno = 'D' THEN mc.Importo ELSE 0 END) AS Dare,
                       SUM(CASE WHEN cc.Segno = 'A' THEN mc.Importo ELSE 0 END) AS Avere
                FROM MovCont mc
                LEFT JOIN CausaliCont cc ON cc.Codice = mc.Causale
                WHERE mc.Anno = @year AND mc.CliFor = @type
                GROUP BY mc.Ditta
            ) m ON m.Ditta = a.Codice
            WHERE (@storeCode IS NULL OR a.PuntoV = @storeCode)
            ORDER BY a.Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@type", type);
        command.Parameters.AddWithValue("@storeCode", storeCode is null ? DBNull.Value : storeCode.Value);

        var rows = new List<CustomerSupplierBalanceSummaryRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var storedInitial = Money(reader["SaldoIniziale"]);
            var initial = type == "F" ? -storedInitial : storedInitial;
            var debit = Money(reader["Dare"]);
            var credit = Money(reader["Avere"]);
            var operations = type == "C" ? debit : credit;
            var settlements = type == "C" ? credit : debit;
            var balance = Math.Round(initial + operations - settlements, 2);
            if (!showZero && balance == 0)
            {
                continue;
            }

            rows.Add(new CustomerSupplierBalanceSummaryRow(
                type,
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Nome"]) ?? "",
                initial,
                operations,
                settlements,
                balance));
        }
        return rows;
    }

    private static decimal Money(object? value) =>
        value is null || value == DBNull.Value ? 0 : Math.Round(Convert.ToDecimal(value), 2);
}
