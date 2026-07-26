using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class CategoryRepository(
    MicronoteDb database,
    ProgressiveCodeService progressiveCodes)
{
    public async Task<IReadOnlyList<CategoryListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Descrizione, '') AS Descrizione
            FROM Categorie
            ORDER BY Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var categories = new List<CategoryListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            categories.Add(new CategoryListItem(
                Convert.ToInt32(reader["Codice"]),
                reader.GetString("Descrizione")));
        }

        return categories;
    }

    public async Task<CategoryEditModel?> GetAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione
            FROM Categorie
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

        return new CategoryEditModel
        {
            Code = Convert.ToInt32(reader["Codice"]),
            Description = reader.GetString("Descrizione")
        };
    }

    public async Task<int> InsertAsync(
        CategoryEditModel category,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        category.Code = await progressiveCodes.NextCodeAsync(
            connection,
            "Categorie",
            "Codice",
            cancellationToken: cancellationToken);

        const string sql = """
            INSERT INTO Categorie (Codice, Descrizione)
            VALUES (@code, @description);
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, category);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return category.Code;
    }

    public Task<int> NextCodeAsync(CancellationToken cancellationToken = default) =>
        progressiveCodes.NextCodeAsync(
            "Categorie",
            "Codice",
            cancellationToken: cancellationToken);

    public async Task<bool> UpdateAsync(
        CategoryEditModel category,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        const string sql = """
            UPDATE Categorie
            SET Descrizione = @description
            WHERE Codice = @code;
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, category);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<CategoryDeleteResult> DeleteAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        await using (var linkedCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM Articoli WHERE Categoria = @code;",
            connection))
        {
            linkedCommand.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(await linkedCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return new CategoryDeleteResult(
                    false,
                    "La categoria non può essere eliminata: esistono articoli collegati.");
            }
        }

        await using var command = new MySqlCommand(
            "DELETE FROM Categorie WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? new CategoryDeleteResult(true, "Categoria eliminata.")
            : new CategoryDeleteResult(false, "Categoria non trovata.");
    }

    private static void AddSaveParameters(MySqlCommand command, CategoryEditModel category)
    {
        command.Parameters.AddWithValue("@code", category.Code);
        command.Parameters.AddWithValue("@description", category.Description.Trim());
    }
}
