using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class GroupRepository(
    MicronoteDb database,
    ProgressiveCodeService progressiveCodes)
{
    public async Task<IReadOnlyList<GroupListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Descrizione, '') AS Descrizione
            FROM Gruppi
            ORDER BY Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var groups = new List<GroupListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            groups.Add(new GroupListItem(
                Convert.ToInt32(reader["Codice"]),
                reader.GetString("Descrizione")));
        }

        return groups;
    }

    public async Task<GroupEditModel?> GetAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione
            FROM Gruppi
            WHERE Codice = @code
            LIMIT 1;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new GroupEditModel
        {
            Code = Convert.ToInt32(reader["Codice"]),
            Description = reader.GetString("Descrizione")
        };
    }

    public async Task<int> InsertAsync(
        GroupEditModel group,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        group.Code = await progressiveCodes.NextCodeAsync(
            connection,
            "Gruppi",
            "Codice",
            cancellationToken: cancellationToken);

        const string sql = """
            INSERT INTO Gruppi (Codice, Descrizione)
            VALUES (@code, @description);
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, group);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return group.Code;
    }

    public Task<int> NextCodeAsync(CancellationToken cancellationToken = default) =>
        progressiveCodes.NextCodeAsync(
            "Gruppi",
            "Codice",
            cancellationToken: cancellationToken);

    public async Task<bool> UpdateAsync(
        GroupEditModel group,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        const string sql = """
            UPDATE Gruppi
            SET Descrizione = @description
            WHERE Codice = @code;
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, group);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<GroupDeleteResult> DeleteAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        await using (var linkedCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM Articoli WHERE Gruppi = @code;",
            connection))
        {
            linkedCommand.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(await linkedCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return new GroupDeleteResult(
                    false,
                    "Il gruppo non può essere eliminato: esistono articoli collegati.");
            }
        }

        await using var command = new MySqlCommand(
            "DELETE FROM Gruppi WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? new GroupDeleteResult(true, "Gruppo eliminato.")
            : new GroupDeleteResult(false, "Gruppo non trovato.");
    }

    private static void AddSaveParameters(MySqlCommand command, GroupEditModel group)
    {
        command.Parameters.AddWithValue("@code", group.Code);
        command.Parameters.AddWithValue("@description", group.Description.Trim());
    }
}
