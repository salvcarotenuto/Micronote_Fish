using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class CashCauseRepository(MicronoteDb database)
{
    public async Task<int> NextCodeAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE
                WHEN NOT EXISTS (SELECT 1 FROM CausaliCassa WHERE Codice = 1) THEN 1
                ELSE COALESCE((
                    SELECT MIN(current_row.Codice + 1)
                    FROM CausaliCassa current_row
                    LEFT JOIN CausaliCassa next_row
                        ON next_row.Codice = current_row.Codice + 1
                    WHERE next_row.Codice IS NULL
                      AND current_row.Codice < 999
                ), 0)
            END;
            """;
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<CashCauseListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione,
                   COALESCE(Tipo, '') AS Tipo, COALESCE(Ditta, '') AS Ditta,
                   COALESCE(Locked, 0) AS Locked
            FROM CausaliCassa
            ORDER BY Codice;
            """;
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<CashCauseListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                Convert.ToInt32(reader["Codice"]),
                reader.GetString("Descrizione"),
                reader.GetString("Tipo"),
                reader.GetString("Ditta"),
                Convert.ToInt32(reader["Locked"]) != 0));
        }
        return rows;
    }

    public async Task<CashCauseEditModel?> GetAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione,
                   COALESCE(Tipo, '') AS Tipo, COALESCE(Ditta, '') AS Ditta,
                   COALESCE(Locked, 0) AS Locked
            FROM CausaliCassa WHERE Codice = @code LIMIT 1;
            """;
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", code);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new CashCauseEditModel
        {
            IsNew = false,
            Code = Convert.ToInt32(reader["Codice"]),
            Description = reader.GetString("Descrizione"),
            MovementType = reader.GetString("Tipo"),
            SubjectType = reader.GetString("Ditta"),
            Locked = Convert.ToInt32(reader["Locked"]) != 0
        };
    }

    public async Task<bool> ExistsAsync(int code, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM CausaliCassa WHERE Codice = @code;", connection);
        command.Parameters.AddWithValue("@code", code);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task InsertAsync(CashCauseEditModel cause, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO CausaliCassa (Codice, Descrizione, Tipo, Ditta, Locked)
            VALUES (@code, @description, @movementType, @subjectType, @locked);
            """;
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        AddParameters(command, cause, includeLocked: true);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<(bool Updated, string Message)> UpdateAsync(
        CashCauseEditModel cause,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using (var locked = new MySqlCommand(
            "SELECT COALESCE(Locked, 0) FROM CausaliCassa WHERE Codice = @code;", connection))
        {
            locked.Parameters.AddWithValue("@code", cause.Code);
            var value = await locked.ExecuteScalarAsync(cancellationToken);
            if (value is null) return (false, "Causale non trovata.");
            if (Convert.ToInt32(value) != 0) return (false, "La causale è bloccata e non può essere modificata.");
        }
        const string sql = """
            UPDATE CausaliCassa
            SET Descrizione = @description, Tipo = @movementType, Ditta = @subjectType
            WHERE Codice = @code;
            """;
        await using var command = new MySqlCommand(sql, connection);
        AddParameters(command, cause, includeLocked: false);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? (true, "Causale aggiornata.")
            : (false, "Causale non trovata.");
    }

    public async Task<CashCauseDeleteResult> DeleteAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using (var locked = new MySqlCommand(
            "SELECT COALESCE(Locked, 0) FROM CausaliCassa WHERE Codice = @code;", connection))
        {
            locked.Parameters.AddWithValue("@code", code);
            var value = await locked.ExecuteScalarAsync(cancellationToken);
            if (value is null) return new(false, "Causale non trovata.");
            if (Convert.ToInt32(value) != 0)
                return new(false, "La causale è bloccata e non può essere eliminata.");
        }
        await using (var linked = new MySqlCommand(
            "SELECT COUNT(*) FROM MovCassa WHERE Causale = @code;", connection))
        {
            linked.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(await linked.ExecuteScalarAsync(cancellationToken)) > 0)
                return new(false, "La causale non può essere eliminata: esistono movimenti di cassa collegati.");
        }
        await using var command = new MySqlCommand(
            "DELETE FROM CausaliCassa WHERE Codice = @code;", connection);
        command.Parameters.AddWithValue("@code", code);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? new(true, "Causale eliminata.")
            : new(false, "Causale non trovata.");
    }

    private static void AddParameters(MySqlCommand command, CashCauseEditModel cause, bool includeLocked)
    {
        command.Parameters.AddWithValue("@code", cause.Code);
        command.Parameters.AddWithValue("@description", cause.Description.Trim());
        command.Parameters.AddWithValue("@movementType", cause.MovementType.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("@subjectType", cause.SubjectType.Trim().ToUpperInvariant());
        if (includeLocked) command.Parameters.AddWithValue("@locked", cause.Locked ? 1 : 0);
    }
}
