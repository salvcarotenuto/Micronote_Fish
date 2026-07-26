using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class InitialArticleStockRepository(MicronoteDb database)
{
    public async Task<IReadOnlyList<InitialArticleStockRow>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                a.Codice,
                COALESCE(a.Descrizione, '') AS Descrizione,
                COALESCE(a.Uma, '') AS Ums,
                COALESCE(c.Descrizione, '') AS Categoria,
                COALESCE(g.Descrizione, '') AS Gruppo,
                COALESCE(a.GiacIn, 0) AS GiacIn
            FROM Articoli a
            LEFT JOIN Categorie c ON c.Codice = a.Categoria
            LEFT JOIN Gruppi g ON g.Codice = a.Gruppo
            ORDER BY a.Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var rows = new List<InitialArticleStockRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new InitialArticleStockRow(
                Convert.ToString(reader["Codice"]) ?? "",
                Convert.ToString(reader["Descrizione"]) ?? "",
                Convert.ToString(reader["Ums"]) ?? "",
                Convert.ToString(reader["Categoria"]) ?? "",
                Convert.ToString(reader["Gruppo"]) ?? "",
                Convert.ToDecimal(reader["GiacIn"])));
        }

        return rows;
    }

    public async Task SaveAsync(
        IReadOnlyList<InitialArticleStockSaveRow> rows,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sql = "UPDATE Articoli SET GiacIn = @quantity WHERE Codice = @code;";
        await using var command = new MySqlCommand(sql, connection, transaction);
        var quantityParameter = command.Parameters.Add("@quantity", MySqlDbType.Decimal);
        var codeParameter = command.Parameters.Add("@code", MySqlDbType.VarChar);

        foreach (var row in rows)
        {
            codeParameter.Value = row.Code.Trim();
            quantityParameter.Value = row.Quantity;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
