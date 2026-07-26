using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class LedgerMasterRepository(
    MicronoteDb database,
    ProgressiveCodeService progressiveCodes)
{
    public static IReadOnlyList<LedgerMasterTypeOption> TypeOptions { get; } =
    [
        new("P", "Patrimoniale"),
        new("C", "Costo"),
        new("R", "Ricavo")
    ];

    public async Task<IReadOnlyList<LedgerMasterListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Descrizione, '') AS Descrizione,
                COALESCE(Tipo, '') AS Tipo,
                COALESCE(Locked, 0) AS Locked
            FROM mastri
            ORDER BY Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var masters = new List<LedgerMasterListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            masters.Add(new LedgerMasterListItem(
                Convert.ToInt32(reader["Codice"]),
                reader.GetString("Descrizione"),
                reader.GetString("Tipo"),
                Convert.ToInt32(reader["Locked"]) != 0));
        }

        return masters;
    }

    public async Task<LedgerMasterEditModel?> GetAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Descrizione, '') AS Descrizione,
                COALESCE(Tipo, '') AS Tipo,
                COALESCE(Locked, 0) AS Locked
            FROM mastri
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

        return new LedgerMasterEditModel
        {
            IsNew = false,
            Code = Convert.ToInt32(reader["Codice"]),
            Description = reader.GetString("Descrizione"),
            Type = reader.GetString("Tipo"),
            Locked = Convert.ToInt32(reader["Locked"]) != 0
        };
    }

    public async Task<bool> ExistsAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM mastri WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public Task<int> NextCodeAsync(CancellationToken cancellationToken = default) =>
        progressiveCodes.NextCodeAsync(
            "mastri",
            "Codice",
            cancellationToken: cancellationToken);

    public async Task InsertAsync(
        LedgerMasterEditModel master,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO mastri (Codice, Descrizione, Tipo, Locked)
            VALUES (@code, @description, @type, @locked);
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        master.Code = await progressiveCodes.NextCodeAsync(
            connection,
            "mastri",
            "Codice",
            cancellationToken: cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, master);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(
        LedgerMasterEditModel master,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE mastri
            SET
                Descrizione = @description,
                Tipo = @type,
                Locked = @locked
            WHERE Codice = @code;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, master);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<LedgerMasterDeleteResult> DeleteAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        await using (var linkedCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM conti WHERE Mastro = @code;",
            connection))
        {
            linkedCommand.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(await linkedCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return new LedgerMasterDeleteResult(
                    false,
                    "Il mastro non può essere eliminato: esistono conti collegati.");
            }
        }

        await using var command = new MySqlCommand(
            "DELETE FROM mastri WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? new LedgerMasterDeleteResult(true, "Mastro eliminato.")
            : new LedgerMasterDeleteResult(false, "Mastro non trovato.");
    }

    private static void AddSaveParameters(MySqlCommand command, LedgerMasterEditModel master)
    {
        command.Parameters.AddWithValue("@code", master.Code);
        command.Parameters.AddWithValue("@description", master.Description.Trim());
        command.Parameters.AddWithValue("@type", master.Type.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("@locked", master.Locked ? 1 : 0);
    }
}
