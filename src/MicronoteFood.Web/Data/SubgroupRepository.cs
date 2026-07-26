using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class SubgroupRepository(
    MicronoteDb database,
    ProgressiveCodeService progressiveCodes)
{
    public async Task<IReadOnlyList<SubgroupListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Descrizione, '') AS Descrizione
            FROM Sottogruppi
            ORDER BY Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var subgroups = new List<SubgroupListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            subgroups.Add(new SubgroupListItem(
                Convert.ToInt32(reader["Codice"]),
                reader.GetString("Descrizione")));
        }

        return subgroups;
    }

    public async Task<SubgroupEditModel?> GetAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione
            FROM Sottogruppi
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

        return new SubgroupEditModel
        {
            Code = Convert.ToInt32(reader["Codice"]),
            Description = reader.GetString("Descrizione")
        };
    }

    public async Task<int> InsertAsync(
        SubgroupEditModel subgroup,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        subgroup.Code = await progressiveCodes.NextCodeAsync(
            connection,
            "Sottogruppi",
            "Codice",
            cancellationToken: cancellationToken);

        const string sql = """
            INSERT INTO Sottogruppi (Codice, Descrizione)
            VALUES (@code, @description);
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, subgroup);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return subgroup.Code;
    }

    public Task<int> NextCodeAsync(CancellationToken cancellationToken = default) =>
        progressiveCodes.NextCodeAsync(
            "Sottogruppi",
            "Codice",
            cancellationToken: cancellationToken);

    public async Task<bool> UpdateAsync(
        SubgroupEditModel subgroup,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        const string sql = """
            UPDATE Sottogruppi
            SET Descrizione = @description
            WHERE Codice = @code;
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, subgroup);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<SubgroupDeleteResult> DeleteAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        await using (var linkedCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM Articoli WHERE Sottogruppo = @code;",
            connection))
        {
            linkedCommand.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(await linkedCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return new SubgroupDeleteResult(
                    false,
                    "Il sottogruppo non può essere eliminato: esistono articoli collegati.");
            }
        }

        await using var command = new MySqlCommand(
            "DELETE FROM Sottogruppi WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? new SubgroupDeleteResult(true, "Sottogruppo eliminato.")
            : new SubgroupDeleteResult(false, "Sottogruppo non trovato.");
    }

    private static void AddSaveParameters(
        MySqlCommand command,
        SubgroupEditModel subgroup)
    {
        command.Parameters.AddWithValue("@code", subgroup.Code);
        command.Parameters.AddWithValue("@description", subgroup.Description.Trim());
    }
}
