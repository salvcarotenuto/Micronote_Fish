using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class InitialArticleStockRepository(MicronoteDb database)
{
    private const string InventoryDateKey = "DataInventario";

    public async Task<InitialArticleStockData> GetAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                a.Codice,
                COALESCE(a.Descrizione, '') AS Descrizione,
                COALESCE(a.Uma, '') AS Ums,
                COALESCE(c.Descrizione, '') AS Categoria,
                COALESCE(g.Descrizione, '') AS Gruppo,
                COALESCE(a.GiacinC, 0) AS GiacinC,
                COALESCE(a.GiacinP, 0) AS GiacinP
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
                Convert.ToInt32(reader["GiacinC"]),
                Convert.ToDecimal(reader["GiacinP"])));
        }

        await reader.DisposeAsync();

        await using var dateCommand = new MySqlCommand(
            "SELECT Valore FROM Opzioni WHERE Chiave = @key LIMIT 1;",
            connection);
        dateCommand.Parameters.AddWithValue("@key", InventoryDateKey);
        var dateValue = Convert.ToString(await dateCommand.ExecuteScalarAsync(cancellationToken))?.Trim();
        DateOnly? inventoryDate = DateOnly.TryParse(dateValue, out var parsedDate) ? parsedDate : null;

        return new InitialArticleStockData(inventoryDate, rows);
    }

    public async Task SaveAsync(
        DateOnly inventoryDate,
        IReadOnlyList<InitialArticleStockSaveRow> rows,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sql = "UPDATE Articoli SET GiacinC = @packages, GiacinP = @quantity WHERE Codice = @code;";
        await using var command = new MySqlCommand(sql, connection, transaction);
        var packagesParameter = command.Parameters.Add("@packages", MySqlDbType.Int32);
        var quantityParameter = command.Parameters.Add("@quantity", MySqlDbType.Decimal);
        var codeParameter = command.Parameters.Add("@code", MySqlDbType.VarChar);

        foreach (var row in rows)
        {
            codeParameter.Value = row.Code.Trim();
            packagesParameter.Value = row.Packages;
            quantityParameter.Value = row.Quantity;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var dateCommand = new MySqlCommand(
            """
            INSERT INTO Opzioni (Chiave, Valore)
            VALUES (@key, @value)
            ON DUPLICATE KEY UPDATE Valore = @value;
            """,
            connection,
            transaction);
        dateCommand.Parameters.AddWithValue("@key", InventoryDateKey);
        dateCommand.Parameters.AddWithValue("@value", inventoryDate.ToString("yyyy-MM-dd"));
        await dateCommand.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
