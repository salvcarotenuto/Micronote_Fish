using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class OpeningBalanceRepository(MicronoteDb database)
{
    private const int OpeningSector = 40;
    private const int OpeningCause = 15;

    public async Task<OpeningBalanceList> ListAsync(
        int year,
        int currentYear,
        CancellationToken cancellationToken = default)
    {
        var movement = await LoadOpeningMovementAsync(year, cancellationToken);
        var rows = await ListRowsAsync(year, movement.Id, cancellationToken);
        var years = Enumerable.Range(currentYear - 1, 3)
            .Reverse()
            .ToArray();

        if (!years.Contains(year))
        {
            years = years.Append(year).OrderDescending().ToArray();
        }

        return new OpeningBalanceList(
            year,
            years,
            movement.MovementDate ?? new DateOnly(year, 1, 1),
            rows);
    }

    public async Task<IReadOnlyList<OpeningBalanceSaveRow>> CalculateBalancesAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.Codice AS AccountCode,
                ROUND(COALESCE(SUM(CASE
                    WHEN mv.ID IS NOT NULL AND r.Segno = 'D' THEN r.Importo
                    WHEN mv.ID IS NOT NULL AND r.Segno = 'A' THEN -r.Importo
                    ELSE 0
                END), 0), 2) AS Balance
            FROM conti c
            INNER JOIN mastri ma ON ma.Codice = c.Mastro AND ma.Tipo = 'P'
            LEFT JOIN movcontrg r ON r.Conto = c.Codice
            LEFT JOIN movcont mv ON mv.ID = r.ID AND mv.Anno = @year
            GROUP BY c.Codice
            ORDER BY c.Mastro, c.Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@year", MySqlDbType.Int32).Value = year;

        var rows = new List<OpeningBalanceSaveRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var accountCode = Convert.ToInt32(reader["AccountCode"]);
            var balance = Convert.ToDecimal(reader["Balance"]);
            rows.Add(balance switch
            {
                > 0 => new OpeningBalanceSaveRow(accountCode, balance, 0),
                < 0 => new OpeningBalanceSaveRow(accountCode, 0, Math.Abs(balance)),
                _ => new OpeningBalanceSaveRow(accountCode, 0, 0)
            });
        }

        return rows;
    }

    public async Task SaveAsync(
        int year,
        DateOnly movementDate,
        IReadOnlyList<OpeningBalanceSaveRow> rows,
        CancellationToken cancellationToken = default)
    {
        var normalizedRows = rows
            .Select(row => new OpeningBalanceSaveRow(
                row.AccountCode,
                Math.Max(row.Debit, 0),
                Math.Max(row.Credit, 0)))
            .Where(row => row.AccountCode > 0 && (row.Debit != 0 || row.Credit != 0))
            .ToArray();

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existingId = await LoadOpeningMovementIdAsync(connection, transaction, year, cancellationToken);
        if (existingId is not null)
        {
            await DeleteMovementAsync(connection, transaction, existingId.Value, cancellationToken);
        }

        if (normalizedRows.Length == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var code = await NextCodeAsync(connection, transaction, year, cancellationToken);
        var totalDebit = normalizedRows.Sum(row => row.Debit);
        var movementId = await InsertMovementAsync(
            connection,
            transaction,
            year,
            code,
            movementDate,
            totalDebit,
            cancellationToken);

        await InsertRowsAsync(
            connection,
            transaction,
            year,
            code,
            movementId,
            normalizedRows,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<OpeningBalanceRow>> ListRowsAsync(
        int year,
        int? movementId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c.Mastro AS MasterCode,
                COALESCE(m.Descrizione, '') AS MasterDescription,
                c.Codice AS AccountCode,
                COALESCE(c.Descrizione, '') AS AccountDescription,
                COALESCE(SUM(CASE WHEN r.Segno = 'D' THEN r.Importo ELSE 0 END), 0) AS Debit,
                COALESCE(SUM(CASE WHEN r.Segno = 'A' THEN r.Importo ELSE 0 END), 0) AS Credit
            FROM conti c
            INNER JOIN mastri m ON m.Codice = c.Mastro AND m.Tipo = 'P'
            LEFT JOIN movcontrg r ON r.Conto = c.Codice AND r.ID = @movementId
            GROUP BY c.Mastro, m.Descrizione, c.Codice, c.Descrizione
            ORDER BY c.Mastro, c.Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@year", MySqlDbType.Int32).Value = year;
        command.Parameters.Add("@movementId", MySqlDbType.Int32).Value = movementId ?? 0;

        var rows = new List<OpeningBalanceRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new OpeningBalanceRow(
                Convert.ToInt32(reader["MasterCode"]),
                Convert.ToString(reader["MasterDescription"]) ?? "",
                Convert.ToInt32(reader["AccountCode"]),
                Convert.ToString(reader["AccountDescription"]) ?? "",
                Convert.ToDecimal(reader["Debit"]),
                Convert.ToDecimal(reader["Credit"])));
        }

        return rows;
    }

    private async Task<OpeningMovementInfo> LoadOpeningMovementAsync(
        int year,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ID, DataMov
            FROM movcont
            WHERE Anno = @year AND Causale = @cause
            ORDER BY ID DESC
            LIMIT 1;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@year", MySqlDbType.Int32).Value = year;
        command.Parameters.Add("@cause", MySqlDbType.Int32).Value = OpeningCause;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new OpeningMovementInfo(null, null);
        }

        return new OpeningMovementInfo(
            Convert.ToInt32(reader["ID"]),
            reader["DataMov"] == DBNull.Value
                ? null
                : DateOnly.FromDateTime(Convert.ToDateTime(reader["DataMov"])));
    }

    private static async Task<int?> LoadOpeningMovementIdAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT ID
            FROM movcont
            WHERE Anno = @year AND Causale = @cause
            ORDER BY ID DESC
            LIMIT 1;
            """,
            connection,
            transaction);
        command.Parameters.Add("@year", MySqlDbType.Int32).Value = year;
        command.Parameters.Add("@cause", MySqlDbType.Int32).Value = OpeningCause;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }

    private static async Task DeleteMovementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int id,
        CancellationToken cancellationToken)
    {
        await using (var rowsCommand = new MySqlCommand(
            "DELETE FROM movcontrg WHERE ID = @id;",
            connection,
            transaction))
        {
            rowsCommand.Parameters.Add("@id", MySqlDbType.Int32).Value = id;
            await rowsCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = new MySqlCommand(
            "DELETE FROM movcont WHERE ID = @id;",
            connection,
            transaction);
        command.Parameters.Add("@id", MySqlDbType.Int32).Value = id;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> NextCodeAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COALESCE(MAX(Codice), 0) + 1 FROM movcont WHERE Anno = @year;",
            connection,
            transaction);
        command.Parameters.Add("@year", MySqlDbType.Int32).Value = year;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> InsertMovementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int code,
        DateOnly movementDate,
        decimal amount,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            INSERT INTO movcont
                (Anno, Settore, Codice, DataMov, NumDoc, Causale, CliFor, Ditta, Importo, Documento, PuntoV)
            VALUES
                (@year, @sector, @code, @movementDate, '', @cause, '', 0, @amount, NULL, 0);
            SELECT LAST_INSERT_ID();
            """,
            connection,
            transaction);
        command.Parameters.Add("@year", MySqlDbType.Int32).Value = year;
        command.Parameters.Add("@sector", MySqlDbType.Int32).Value = OpeningSector;
        command.Parameters.Add("@code", MySqlDbType.Int32).Value = code;
        command.Parameters.Add("@movementDate", MySqlDbType.Date).Value = movementDate.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@cause", MySqlDbType.Int32).Value = OpeningCause;
        command.Parameters.Add("@amount", MySqlDbType.Decimal).Value = amount;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task InsertRowsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        int code,
        int movementId,
        IReadOnlyList<OpeningBalanceSaveRow> rows,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO movcontrg
                (ID, Anno, Settore, Codice, Riga, Conto, Importo, Segno)
            VALUES
                (@id, @year, @sector, @code, @rowNumber, @accountCode, @amount, @sign);
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        var idParameter = command.Parameters.Add("@id", MySqlDbType.Int32);
        var yearParameter = command.Parameters.Add("@year", MySqlDbType.Int32);
        var sectorParameter = command.Parameters.Add("@sector", MySqlDbType.Int32);
        var codeParameter = command.Parameters.Add("@code", MySqlDbType.Int32);
        var rowNumberParameter = command.Parameters.Add("@rowNumber", MySqlDbType.Int32);
        var accountCodeParameter = command.Parameters.Add("@accountCode", MySqlDbType.Int32);
        var amountParameter = command.Parameters.Add("@amount", MySqlDbType.Decimal);
        var signParameter = command.Parameters.Add("@sign", MySqlDbType.VarChar);

        var rowNumber = 1;
        foreach (var row in rows)
        {
            var sign = row.Debit != 0 ? "D" : "A";
            var amount = row.Debit != 0 ? row.Debit : row.Credit;

            idParameter.Value = movementId;
            yearParameter.Value = year;
            sectorParameter.Value = OpeningSector;
            codeParameter.Value = code;
            rowNumberParameter.Value = rowNumber++;
            accountCodeParameter.Value = row.AccountCode;
            amountParameter.Value = amount;
            signParameter.Value = sign;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private sealed record OpeningMovementInfo(int? Id, DateOnly? MovementDate);
}
