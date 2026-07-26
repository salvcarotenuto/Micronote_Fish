using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class ComuniRepository(MicronoteDb database)
{
    public async Task<IReadOnlyList<ComuneListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                COALESCE(Nome, '') AS Nome,
                COALESCE(Prov, '') AS Prov,
                COALESCE(Cap, '') AS Cap,
                COALESCE(Codice, '') AS Codice
            FROM comuni
            ORDER BY Nome, Prov;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var comuni = new List<ComuneListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            comuni.Add(new ComuneListItem(
                reader.GetString("Nome"),
                reader.GetString("Prov"),
                reader.GetString("Cap"),
                reader.GetString("Codice")));
        }

        return comuni;
    }

    public async Task<ComuneEditModel?> GetAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                COALESCE(Nome, '') AS Nome,
                COALESCE(Prov, '') AS Prov,
                COALESCE(Cap, '') AS Cap,
                COALESCE(Codice, '') AS Codice
            FROM comuni
            WHERE Nome = @name
            LIMIT 1;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@name", name);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var comuneName = reader.GetString("Nome");
        return new ComuneEditModel
        {
            IsNew = false,
            OriginalName = comuneName,
            Name = comuneName,
            Province = reader.GetString("Prov"),
            PostalCode = reader.GetString("Cap"),
            Code = reader.GetString("Codice")
        };
    }

    public async Task<bool> ExistsAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM comuni WHERE Nome = @name;",
            connection);
        command.Parameters.AddWithValue("@name", name);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task InsertAsync(
        ComuneEditModel comune,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO comuni (Nome, Prov, Cap, Codice)
            VALUES (@name, @province, @postalCode, @code);
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, comune);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(
        ComuneEditModel comune,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE comuni
            SET
                Nome = @name,
                Prov = @province,
                Cap = @postalCode,
                Codice = @code
            WHERE Nome = @originalName;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, comune);
        command.Parameters.AddWithValue("@originalName", comune.OriginalName.Trim());

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<ComuneDeleteResult> DeleteAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "DELETE FROM comuni WHERE Nome = @name;",
            connection);
        command.Parameters.AddWithValue("@name", name);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0
            ? new ComuneDeleteResult(true, "Comune eliminato.")
            : new ComuneDeleteResult(false, "Comune non trovato.");
    }

    public async Task<IReadOnlyList<ComuneSuggestion>> SearchAsync(
        string? query,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query?.Trim() ?? "";
        if (normalizedQuery.Length < 2)
        {
            return [];
        }

        const string sql = """
            SELECT
                COALESCE(Nome, '') AS Nome,
                COALESCE(Prov, '') AS Prov,
                COALESCE(Cap, '') AS Cap
            FROM comuni
            WHERE Nome LIKE @prefixWhere OR Nome LIKE @contains
            ORDER BY
                CASE WHEN Nome LIKE @prefixOrder THEN 0 ELSE 1 END,
                Nome,
                Prov
            LIMIT 12;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@prefixWhere", $"{normalizedQuery}%");
        command.Parameters.AddWithValue("@contains", $"%{normalizedQuery}%");
        command.Parameters.AddWithValue("@prefixOrder", $"{normalizedQuery}%");

        var comuni = new List<ComuneSuggestion>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            comuni.Add(new ComuneSuggestion(
                reader.GetString("Nome"),
                reader.GetString("Prov"),
                reader.GetString("Cap")));
        }

        return comuni;
    }

    private static void AddSaveParameters(MySqlCommand command, ComuneEditModel comune)
    {
        command.Parameters.AddWithValue("@name", comune.Name.Trim());
        command.Parameters.AddWithValue("@province", DbText(comune.Province?.Trim().ToUpperInvariant()));
        command.Parameters.AddWithValue("@postalCode", DbText(comune.PostalCode?.Trim()));
        command.Parameters.AddWithValue("@code", DbText(comune.Code?.Trim()));
    }

    private static object DbText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
}
