using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class CashStatementRepository(MicronoteDb database)
{
    public async Task<CashStatementPageModel> GetAsync(
        DateOnly dateFrom,
        DateOnly dateTo,
        int cashType,
        int storeCode,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var stores = await ListStoresAsync(connection, cancellationToken);
        var accounts = cashType switch
        {
            61 => new[] { 61 },
            65 => new[] { 65 },
            _ => new[] { 60, 61 }
        };

        const string sql = """
            SELECT m.ID, m.Anno, m.Settore, m.Codice, m.DataMov,
                   COALESCE(m.NumDoc, '') AS NumDoc,
                   NULLIF(m.Causale, 0) AS Causale,
                   COALESCE(cc.Descrizione, '') AS DecCausale,
                   CASE WHEN r.Segno = 'D' THEN COALESCE(r.Importo, 0) ELSE 0 END AS Entrate,
                   CASE WHEN r.Segno = 'A' THEN COALESCE(r.Importo, 0) ELSE 0 END AS Uscite,
                   r.Conto, COALESCE(ct.Descrizione, '') AS DecConto,
                   COALESCE(pv.Nome, '') AS PuntoVendita
            FROM MovCont m
            INNER JOIN MovContRg r ON r.ID = m.ID
            LEFT JOIN Conti ct ON ct.Codice = r.Conto
            LEFT JOIN CausaliCont cc ON cc.Codice = m.Causale
            LEFT JOIN PuntiVendita pv ON pv.Codice = m.PuntoV
            WHERE r.Conto IN (@account1, @account2)
              AND m.DataMov BETWEEN @dateFrom AND @dateTo
              AND (@storeCode = 0 OR m.PuntoV = @storeCode)
            ORDER BY m.DataMov, m.Codice, r.Riga;
            """;

        var rows = new List<CashStatementRow>();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@account1", accounts[0]);
        command.Parameters.AddWithValue("@account2", accounts.Length > 1 ? accounts[1] : accounts[0]);
        command.Parameters.Add("@dateFrom", MySqlDbType.Date).Value = dateFrom.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@dateTo", MySqlDbType.Date).Value = dateTo.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@storeCode", storeCode);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CashStatementRow(
                Convert.ToInt32(reader["ID"]),
                Convert.ToInt32(reader["Anno"]),
                Convert.ToInt32(reader["Settore"]),
                Convert.ToInt32(reader["Codice"]),
                DateOnly.FromDateTime(Convert.ToDateTime(reader["DataMov"])),
                Convert.ToString(reader["NumDoc"]) ?? "",
                reader["Causale"] is DBNull ? null : Convert.ToInt32(reader["Causale"]),
                Convert.ToString(reader["DecCausale"]) ?? "",
                Money(reader["Entrate"]), Money(reader["Uscite"]),
                Convert.ToInt32(reader["Conto"]),
                Convert.ToString(reader["DecConto"]) ?? "",
                Convert.ToString(reader["PuntoVendita"]) ?? ""));
        }

        return new CashStatementPageModel
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            CashType = cashType,
            StoreCode = storeCode,
            Stores = stores,
            Rows = rows,
            TotalIncome = rows.Sum(row => row.Income),
            TotalExpense = rows.Sum(row => row.Expense)
        };
    }

    private static async Task<IReadOnlyList<CashStatementStore>> ListStoresAsync(
        MySqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "SELECT Codice, COALESCE(Nome, '') AS Nome FROM PuntiVendita ORDER BY Codice;";
        var rows = new List<CashStatementStore>();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new(Convert.ToInt32(reader["Codice"]), Convert.ToString(reader["Nome"]) ?? ""));
        return rows;
    }

    private static decimal Money(object value) => value is DBNull ? 0 : Convert.ToDecimal(value);
}
