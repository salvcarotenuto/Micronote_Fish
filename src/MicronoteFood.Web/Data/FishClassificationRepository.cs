using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class FishClassificationRepository(
    MicronoteDb database,
    ProgressiveCodeService progressiveCodes)
{
    public Task<IReadOnlyList<FishClassificationListItem>> ListSpeciesAsync(
        CancellationToken cancellationToken = default) =>
        ListAsync("Specie", cancellationToken);

    public Task<IReadOnlyList<FishClassificationListItem>> ListOriginsAsync(
        CancellationToken cancellationToken = default) =>
        ListAsync("Provenienza", cancellationToken);

    public Task<FishClassificationEditModel?> GetSpeciesAsync(
        int code,
        CancellationToken cancellationToken = default) =>
        GetAsync("Specie", code, cancellationToken);

    public Task<FishClassificationEditModel?> GetOriginAsync(
        int code,
        CancellationToken cancellationToken = default) =>
        GetAsync("Provenienza", code, cancellationToken);

    public Task<int> InsertSpeciesAsync(
        FishClassificationEditModel value,
        CancellationToken cancellationToken = default) =>
        InsertAsync("Specie", value, cancellationToken);

    public Task<int> InsertOriginAsync(
        FishClassificationEditModel value,
        CancellationToken cancellationToken = default) =>
        InsertAsync("Provenienza", value, cancellationToken);

    public Task<int> NextSpeciesCodeAsync(
        CancellationToken cancellationToken = default) =>
        NextCodeAsync("Specie", cancellationToken);

    public Task<int> NextOriginCodeAsync(
        CancellationToken cancellationToken = default) =>
        NextCodeAsync("Provenienza", cancellationToken);

    public Task<bool> UpdateSpeciesAsync(
        FishClassificationEditModel value,
        CancellationToken cancellationToken = default) =>
        UpdateAsync("Specie", value, cancellationToken);

    public Task<bool> UpdateOriginAsync(
        FishClassificationEditModel value,
        CancellationToken cancellationToken = default) =>
        UpdateAsync("Provenienza", value, cancellationToken);

    public Task<FishClassificationDeleteResult> DeleteSpeciesAsync(
        int code,
        CancellationToken cancellationToken = default) =>
        DeleteAsync("Specie", "Specie", "La specie", code, cancellationToken);

    public Task<FishClassificationDeleteResult> DeleteOriginAsync(
        int code,
        CancellationToken cancellationToken = default) =>
        DeleteAsync(
            "Provenienza",
            "Provenienza",
            "La provenienza",
            code,
            cancellationToken);

    private async Task<IReadOnlyList<FishClassificationListItem>> ListAsync(
        string table,
        CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            $"SELECT Codice, COALESCE(Descrizione, '') AS Descrizione " +
            $"FROM `{table}` ORDER BY Codice;",
            connection);
        var values = new List<FishClassificationListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new(
                Convert.ToInt32(reader["Codice"]),
                reader.GetString("Descrizione")));
        }

        return values;
    }

    private async Task<FishClassificationEditModel?> GetAsync(
        string table,
        int code,
        CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            $"SELECT Codice, COALESCE(Descrizione, '') AS Descrizione " +
            $"FROM `{table}` WHERE Codice = @code LIMIT 1;",
            connection);
        command.Parameters.AddWithValue("@code", code);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new FishClassificationEditModel
            {
                Code = Convert.ToInt32(reader["Codice"]),
                Description = reader.GetString("Descrizione")
            }
            : null;
    }

    private async Task<int> InsertAsync(
        string table,
        FishClassificationEditModel value,
        CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        value.Code = await progressiveCodes.NextCodeAsync(
            connection,
            table,
            "Codice",
            cancellationToken: cancellationToken);
        await using var command = new MySqlCommand(
            $"INSERT INTO `{table}` (Codice, Descrizione) " +
            "VALUES (@code, @description);",
            connection);
        AddParameters(command, value);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return value.Code;
    }

    private Task<int> NextCodeAsync(
        string table,
        CancellationToken cancellationToken) =>
        progressiveCodes.NextCodeAsync(
            table,
            "Codice",
            cancellationToken: cancellationToken);

    private async Task<bool> UpdateAsync(
        string table,
        FishClassificationEditModel value,
        CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            $"UPDATE `{table}` SET Descrizione = @description WHERE Codice = @code;",
            connection);
        AddParameters(command, value);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private async Task<FishClassificationDeleteResult> DeleteAsync(
        string table,
        string articleColumn,
        string label,
        int code,
        CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using (var columnCommand = new MySqlCommand(
            """
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Articoli'
              AND COLUMN_NAME = @column;
            """,
            connection))
        {
            columnCommand.Parameters.AddWithValue("@column", articleColumn);
            if (Convert.ToInt32(
                    await columnCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                await using var linkedCommand = new MySqlCommand(
                    $"SELECT COUNT(*) FROM Articoli WHERE `{articleColumn}` = @code;",
                    connection);
                linkedCommand.Parameters.AddWithValue("@code", code);
                if (Convert.ToInt32(
                        await linkedCommand.ExecuteScalarAsync(cancellationToken)) > 0)
                {
                    return new(
                        false,
                        $"{label} non può essere eliminata: esistono articoli collegati.");
                }
            }
        }

        await using var command = new MySqlCommand(
            $"DELETE FROM `{table}` WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? new(true, $"{label} è stata eliminata.")
            : new(false, $"{label} non è stata trovata.");
    }

    private static void AddParameters(
        MySqlCommand command,
        FishClassificationEditModel value)
    {
        command.Parameters.AddWithValue("@code", value.Code);
        command.Parameters.AddWithValue("@description", value.Description.Trim());
    }
}
