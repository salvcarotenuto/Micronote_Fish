using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class SectorRepository(
    MicronoteDb database,
    ProgressiveCodeService progressiveCodes)
{
    public async Task<IReadOnlyList<SectorListItem>> SearchAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Descrizione, '') AS Descrizione
            FROM Settori
            WHERE @search = '' OR Descrizione LIKE CONCAT('%', @search, '%')
            ORDER BY Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@search", search?.Trim() ?? "");

        var sectors = new List<SectorListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sectors.Add(new SectorListItem(
                Convert.ToInt32(reader["Codice"]),
                reader.GetString("Descrizione")));
        }

        return sectors;
    }

    public async Task<SectorEditModel?> GetAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione
            FROM Settori
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

        return new SectorEditModel
        {
            Code = Convert.ToInt32(reader["Codice"]),
            Description = reader.GetString("Descrizione")
        };
    }

    public async Task<int> InsertAsync(
        SectorEditModel sector,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        sector.Code = await progressiveCodes.NextCodeAsync(
            connection,
            "Settori",
            "Codice",
            cancellationToken: cancellationToken);

        const string sql = """
            INSERT INTO Settori (Codice, Descrizione)
            VALUES (@code, @description);
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, sector);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return sector.Code;
    }

    public Task<int> NextCodeAsync(CancellationToken cancellationToken = default) =>
        progressiveCodes.NextCodeAsync(
            "Settori",
            "Codice",
            cancellationToken: cancellationToken);

    public async Task<bool> UpdateAsync(
        SectorEditModel sector,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        const string sql = """
            UPDATE Settori
            SET Descrizione = @description
            WHERE Codice = @code;
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, sector);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<SectorDeleteResult> DeleteAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "DELETE FROM Settori WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? new SectorDeleteResult(true, "Settore eliminato.")
            : new SectorDeleteResult(false, "Settore non trovato.");
    }

    private static void AddSaveParameters(MySqlCommand command, SectorEditModel sector)
    {
        command.Parameters.AddWithValue("@code", sector.Code);
        command.Parameters.AddWithValue("@description", sector.Description.Trim());
    }
}
