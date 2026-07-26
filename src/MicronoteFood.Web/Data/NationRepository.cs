using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class NationRepository(MicronoteDb database)
{
    public async Task<IReadOnlyList<NationListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Nome, '') AS Nome,
                COALESCE(Sigla2, '') AS Sigla2,
                COALESCE(Sigla3, '') AS Sigla3,
                COALESCE(Iso, '') AS Iso,
                COALESCE(Codfi, '') AS Codfi,
                COALESCE(Zona, '') AS Zona,
                COALESCE(Regime, '') AS Regime
            FROM nazioni
            ORDER BY Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var nations = new List<NationListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            nations.Add(new NationListItem(
                Convert.ToInt32(reader["Codice"]),
                reader.GetString("Nome"),
                reader.GetString("Sigla2"),
                reader.GetString("Sigla3"),
                reader.GetString("Iso"),
                reader.GetString("Codfi"),
                reader.GetString("Zona"),
                reader.GetString("Regime")));
        }

        return nations;
    }

    public async Task<NationEditModel?> GetAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Nome, '') AS Nome,
                COALESCE(Sigla2, '') AS Sigla2,
                COALESCE(Sigla3, '') AS Sigla3,
                COALESCE(Iso, '') AS Iso,
                COALESCE(Codfi, '') AS Codfi,
                COALESCE(Zona, '') AS Zona,
                COALESCE(Regime, '') AS Regime
            FROM nazioni
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

        return new NationEditModel
        {
            IsNew = false,
            Code = Convert.ToInt32(reader["Codice"]),
            Name = reader.GetString("Nome"),
            Abbreviation1 = reader.GetString("Sigla2"),
            Abbreviation2 = reader.GetString("Sigla3"),
            Iso = reader.GetString("Iso"),
            TaxCode = reader.GetString("Codfi"),
            Zone = reader.GetString("Zona"),
            Regime = reader.GetString("Regime")
        };
    }

    public async Task<int> InsertAsync(
        NationEditModel nation,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        nation.Code = await GetNextNationCodeAsync(connection, cancellationToken);

        const string sql = """
            INSERT INTO nazioni (Codice, Nome, Sigla2, Sigla3, Iso, Codfi, Zona, Regime)
            VALUES (@code, @name, @abbreviation1, @abbreviation2, @iso, @taxCode, @zone, @regime);
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, nation);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return nation.Code;
    }

    public async Task<bool> UpdateAsync(
        NationEditModel nation,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE nazioni
            SET
                Nome = @name,
                Sigla2 = @abbreviation1,
                Sigla3 = @abbreviation2,
                Iso = @iso,
                Codfi = @taxCode,
                Zona = @zone,
                Regime = @regime
            WHERE Codice = @code;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, nation);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<NationDeleteResult> DeleteAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "DELETE FROM nazioni WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? new NationDeleteResult(true, "Nazione eliminata.")
            : new NationDeleteResult(false, "Nazione non trovata.");
    }

    private static async Task<int> GetNextNationCodeAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT Codice FROM nazioni ORDER BY Codice;",
            connection);

        var nextCode = 1;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var currentCode = Convert.ToInt32(reader["Codice"]);
            if (currentCode > nextCode)
            {
                break;
            }

            if (currentCode == nextCode)
            {
                nextCode += 1;
            }
        }

        return nextCode;
    }

    private static void AddSaveParameters(MySqlCommand command, NationEditModel nation)
    {
        command.Parameters.AddWithValue("@code", nation.Code);
        command.Parameters.AddWithValue("@name", nation.Name.Trim());
        command.Parameters.AddWithValue("@abbreviation1", DbText(nation.Abbreviation1));
        command.Parameters.AddWithValue("@abbreviation2", DbText(nation.Abbreviation2));
        command.Parameters.AddWithValue("@iso", DbText(nation.Iso));
        command.Parameters.AddWithValue("@taxCode", DbText(nation.TaxCode));
        command.Parameters.AddWithValue("@zone", DbText(nation.Zone));
        command.Parameters.AddWithValue("@regime", DbText(nation.Regime));
    }

    private static object DbText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim().ToUpperInvariant();
}
