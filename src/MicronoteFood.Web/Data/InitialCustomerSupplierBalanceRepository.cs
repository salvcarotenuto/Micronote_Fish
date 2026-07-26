using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class InitialCustomerSupplierBalanceRepository(MicronoteDb database)
{
    public async Task<InitialCustomerSupplierBalanceList> ListAsync(
        int year,
        int currentYear,
        CancellationToken cancellationToken = default)
    {
        var customers = await ListRowsAsync("C", "Clienti", year, cancellationToken);
        var suppliers = await ListRowsAsync("F", "Fornitori", year, cancellationToken);
        var years = Enumerable.Range(currentYear - 4, 5)
            .Reverse()
            .ToArray();

        if (!years.Contains(year))
        {
            years = years.Append(year).OrderDescending().ToArray();
        }

        return new InitialCustomerSupplierBalanceList(year, years, customers, suppliers);
    }

    public async Task SaveAsync(
        int year,
        IReadOnlyList<InitialCustomerSupplierBalanceSaveRow> rows,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var delete = new MySqlCommand(
            "DELETE FROM SaldoIniCf WHERE Anno = @year;",
            connection,
            transaction))
        {
            delete.Parameters.Add("@year", MySqlDbType.Int32).Value = year;
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        const string sql = """
            INSERT INTO SaldoIniCf (Anno, CliFor, Ditta, Importo)
            VALUES (@year, @type, @code, @balance);
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        var yearParameter = command.Parameters.Add("@year", MySqlDbType.Int32);
        var typeParameter = command.Parameters.Add("@type", MySqlDbType.VarChar);
        var codeParameter = command.Parameters.Add("@code", MySqlDbType.Int32);
        var balanceParameter = command.Parameters.Add("@balance", MySqlDbType.Decimal);

        foreach (var row in rows.Where(row => row.Type is "C" or "F"))
        {
            yearParameter.Value = year;
            typeParameter.Value = row.Type;
            codeParameter.Value = row.Code;
            balanceParameter.Value = row.Balance;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<InitialCustomerSupplierBalanceRow>> ListRowsAsync(
        string type,
        string table,
        int year,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT
                a.Codice,
                COALESCE(a.Nome, '') AS Nome,
                COALESCE(a.Citta, '') AS Citta,
                COALESCE(a.Piva, '') AS Piva,
                COALESCE(s.Importo, 0) AS Importo
            FROM {table} a
            LEFT JOIN SaldoIniCf s
                ON s.Anno = @year
               AND s.CliFor = @type
               AND s.Ditta = a.Codice
            ORDER BY a.Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@year", MySqlDbType.Int32).Value = year;
        command.Parameters.Add("@type", MySqlDbType.VarChar).Value = type;

        var rows = new List<InitialCustomerSupplierBalanceRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new InitialCustomerSupplierBalanceRow(
                type,
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Nome"]) ?? "",
                Convert.ToString(reader["Citta"]) ?? "",
                Convert.ToString(reader["Piva"]) ?? "",
                Convert.ToDecimal(reader["Importo"])));
        }

        return rows;
    }
}
