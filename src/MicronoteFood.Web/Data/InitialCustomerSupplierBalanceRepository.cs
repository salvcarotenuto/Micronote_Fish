using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class InitialCustomerSupplierBalanceRepository(MicronoteDb database)
{
    private const string ReferenceYearKey = "AnnoSaldoIniCF";

    public async Task<InitialCustomerSupplierBalanceList> ListAsync(
        int? selectedYear,
        int currentYear,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var requestedYear = selectedYear.GetValueOrDefault();
        var storedYear = requestedYear > 0
            ? requestedYear
            : await ReadReferenceYearAsync(connection, cancellationToken);
        var years = Enumerable.Range(currentYear - 4, 5).Reverse().ToList();
        if (!years.Contains(storedYear))
            years.Add(storedYear);
        years.Sort((left, right) => right.CompareTo(left));
        var customers = await ListRowsAsync(connection, "C", "Clienti", cancellationToken);
        var suppliers = await ListRowsAsync(connection, "F", "Fornitori", cancellationToken);
        return new InitialCustomerSupplierBalanceList(storedYear, years, customers, suppliers);
    }

    public async Task SaveAsync(
        int year,
        IReadOnlyList<InitialCustomerSupplierBalanceSaveRow> rows,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var type in new[] { "C", "F" })
        {
            var table = type == "C" ? "Clienti" : "Fornitori";
            var sql = $"UPDATE {table} SET SaldoIni = @balance WHERE Codice = @code;";
            await using var command = new MySqlCommand(sql, connection, transaction);
            var balance = command.Parameters.Add("@balance", MySqlDbType.Decimal);
            var code = command.Parameters.Add("@code", MySqlDbType.Int32);
            foreach (var row in rows.Where(row => row.Type == type))
            {
                balance.Value = row.Balance;
                code.Value = row.Code;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        const string optionSql = """
            INSERT INTO Opzioni (Chiave, Valore) VALUES (@key, @value)
            ON DUPLICATE KEY UPDATE Valore = @value;
            """;
        await using (var option = new MySqlCommand(optionSql, connection, transaction))
        {
            option.Parameters.AddWithValue("@key", ReferenceYearKey);
            option.Parameters.AddWithValue("@value", year.ToString());
            await option.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<InitialCustomerSupplierBalanceRow>> ListRowsAsync(
        MySqlConnection connection,
        string type,
        string table,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT
                a.Codice,
                COALESCE(a.Nome, '') AS Nome,
                COALESCE(a.Citta, '') AS Citta,
                COALESCE(a.Piva, '') AS Piva,
                COALESCE(a.SaldoIni, 0) AS Importo
            FROM {table} a
            ORDER BY a.Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);

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

    private static async Task<int> ReadReferenceYearAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT Valore FROM Opzioni WHERE Chiave = @key LIMIT 1;", connection);
        command.Parameters.AddWithValue("@key", ReferenceYearKey);
        var value = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
        return int.TryParse(value, out var year) && year > 0 ? year : 2000;
    }
}
