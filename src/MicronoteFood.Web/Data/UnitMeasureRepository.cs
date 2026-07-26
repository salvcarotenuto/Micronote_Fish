using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class UnitMeasureRepository(MicronoteDb database)
{
    public async Task<IReadOnlyList<UnitMeasureListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Descrizione, '') AS Descrizione
            FROM UMisura
            ORDER BY Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var units = new List<UnitMeasureListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            units.Add(new UnitMeasureListItem(
                Convert.ToString(reader["Codice"]) ?? "",
                reader.GetString("Descrizione")));
        }

        return units;
    }

    public async Task<UnitMeasureEditModel?> GetAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione
            FROM UMisura
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

        return new UnitMeasureEditModel
        {
            Code = Convert.ToString(reader["Codice"]) ?? "",
            Description = reader.GetString("Descrizione")
        };
    }

    public async Task<bool> ExistsAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM UMisura
            WHERE Codice = @code;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", code);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task<string> InsertAsync(
        UnitMeasureEditModel unit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO UMisura (Codice, Descrizione)
            VALUES (@code, @description);
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, unit);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return unit.Code;
    }

    public async Task<bool> UpdateAsync(
        UnitMeasureEditModel unit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        const string sql = """
            UPDATE UMisura
            SET Descrizione = @description
            WHERE Codice = @code;
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, unit);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<UnitMeasureDeleteResult> DeleteAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        await using (var linkedCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM Articoli WHERE Um = @code;",
            connection))
        {
            linkedCommand.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(await linkedCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return new UnitMeasureDeleteResult(
                    false,
                    "L'unità di misura non può essere eliminata: esistono articoli collegati.");
            }
        }

        await using var command = new MySqlCommand(
            "DELETE FROM UMisura WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? new UnitMeasureDeleteResult(true, "Unità di misura eliminata.")
            : new UnitMeasureDeleteResult(false, "Unità di misura non trovata.");
    }

    private static void AddSaveParameters(MySqlCommand command, UnitMeasureEditModel unit)
    {
        command.Parameters.AddWithValue("@code", unit.Code.Trim());
        command.Parameters.AddWithValue("@description", unit.Description.Trim());
    }
}
