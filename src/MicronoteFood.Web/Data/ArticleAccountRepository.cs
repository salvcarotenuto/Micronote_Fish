using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class ArticleAccountRepository(MicronoteDb database)
{
    public async Task<ArticleAccountPageModel> GetAsync(
        int year,
        int month,
        string? articleCode,
        string? selectedKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedArticle = (articleCode ?? "").Trim();
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var years = await ListYearsAsync(connection, year, cancellationToken);
        var article = normalizedArticle.Length == 0
            ? new ArticleAccountArticle("", "", 0, 0)
            : await LoadArticleAsync(connection, normalizedArticle, cancellationToken);
        var movements = normalizedArticle.Length == 0
            ? []
            : await ListMovementsAsync(connection, year, month, normalizedArticle, cancellationToken);
        var selected = SelectMovement(movements, selectedKey);

        return new ArticleAccountPageModel
        {
            Year = year,
            Month = Math.Clamp(month, 0, 12),
            ArticleCode = normalizedArticle,
            ArticleDescription = article.Description,
            SelectedKey = selected?.Key,
            Years = years,
            Movements = movements,
            Totals = CalculateTotals(article, movements)
        };
    }

    private static async Task<IReadOnlyList<int>> ListYearsAsync(
        MySqlConnection connection,
        int currentYear,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT Anno
            FROM Movimenti
            WHERE Anno IS NOT NULL
            ORDER BY Anno DESC;
            """;

        var years = new List<int>();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            years.Add(Convert.ToInt32(reader["Anno"]));
        }

        if (!years.Contains(currentYear))
        {
            years.Insert(0, currentYear);
        }

        return years;
    }

    private static async Task<ArticleAccountArticle> LoadArticleAsync(
        MySqlConnection connection,
        string articleCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(Codice, '') AS Codice,
                   COALESCE(Descrizione, '') AS Descrizione,
                   COALESCE(GiacIn, 0) AS GiacIn,
                   COALESCE(CostoStd, 0) AS CostoStd
            FROM Articoli
            WHERE Codice = @articleCode
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@articleCode", articleCode);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new ArticleAccountArticle(articleCode, "", 0, 0);
        }

        return new ArticleAccountArticle(
            Convert.ToString(reader["Codice"]) ?? articleCode,
            Convert.ToString(reader["Descrizione"]) ?? "",
            Decimal(reader["GiacIn"]),
            Decimal(reader["CostoStd"]));
    }

    private static async Task<IReadOnlyList<ArticleAccountMovementItem>> ListMovementsAsync(
        MySqlConnection connection,
        int year,
        int month,
        string articleCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT M.Anno,
                   M.Settore,
                   M.Codice,
                   M.Riga,
                   M.DataMov,
                   COALESCE(M.NumDoc, '') AS NumDoc,
                   COALESCE(M.TipoMov, '') AS TipoMov,
                   COALESCE(M.Quantita, 0) AS Quantita,
                   COALESCE(M.Prezzo, 0) AS Prezzo,
                   COALESCE(M.Importo, 0) AS Importo,
                   COALESCE(M.CliFor, '') AS CliFor,
                   COALESCE(M.Ditta, 0) AS Ditta,
                   CASE
                       WHEN M.CliFor = 'C' THEN COALESCE(Clienti.Nome, '')
                       WHEN M.CliFor = 'F' THEN COALESCE(Fornitori.Nome, '')
                       ELSE ''
                   END AS Nome,
                   COALESCE(Carico.ID, 0) AS StockLoadId,
                   COALESCE(Vendite.ID, 0) AS SaleId
            FROM Movimenti AS M
            LEFT JOIN Clienti ON Clienti.Codice = M.Ditta
                AND M.CliFor = 'C'
            LEFT JOIN Fornitori ON Fornitori.Codice = M.Ditta
                AND M.CliFor = 'F'
            LEFT JOIN Carico ON Carico.Anno = M.Anno
                AND Carico.Codice = M.Codice
                AND M.Settore = 10
            LEFT JOIN Vendite ON Vendite.Anno = M.Anno
                AND Vendite.Codice = M.Codice
                AND M.Settore = 30
            WHERE M.Articolo = @articleCode
              AND M.Anno = @year
              AND (@month = 0 OR MONTH(M.DataMov) = @month)
            ORDER BY M.DataMov, M.Settore, M.Codice, M.Riga;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@articleCode", articleCode);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@month", Math.Clamp(month, 0, 12));

        var rows = new List<ArticleAccountMovementItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ArticleAccountMovementItem(
                Convert.ToInt32(reader["Anno"]),
                Convert.ToInt32(reader["Settore"]),
                Convert.ToInt32(reader["Codice"]),
                Convert.ToInt32(reader["Riga"]),
                DateOnly.FromDateTime(Convert.ToDateTime(reader["DataMov"])),
                Convert.ToString(reader["NumDoc"]) ?? "",
                Convert.ToString(reader["TipoMov"]) ?? "",
                Decimal(reader["Quantita"]),
                Decimal(reader["Prezzo"]),
                Decimal(reader["Importo"]),
                Convert.ToString(reader["CliFor"]) ?? "",
                Convert.ToInt32(reader["Ditta"]),
                Convert.ToString(reader["Nome"]) ?? "",
                Convert.ToInt32(reader["StockLoadId"]),
                Convert.ToInt32(reader["SaleId"])));
        }

        return rows;
    }

    private static ArticleAccountTotals CalculateTotals(
        ArticleAccountArticle article,
        IReadOnlyList<ArticleAccountMovementItem> movements)
    {
        var loadQuantity = movements.Sum(movement => movement.LoadQuantity);
        var loadValue = movements.Sum(movement => movement.LoadAmount);
        var unloadQuantity = movements.Sum(movement => movement.UnloadQuantity);
        var unloadValue = movements.Sum(movement => movement.UnloadAmount);
        var loadAverageCost = loadQuantity == 0 ? 0 : loadValue / loadQuantity;
        var unloadAveragePrice = unloadQuantity == 0 ? 0 : unloadValue / unloadQuantity;
        var finalQuantity = article.InitialQuantity + loadQuantity - unloadQuantity;
        var finalUnitValue = loadAverageCost == 0 ? article.StandardCost : loadAverageCost;

        return new ArticleAccountTotals
        {
            InitialQuantity = article.InitialQuantity,
            InitialValue = article.InitialQuantity * article.StandardCost,
            LoadQuantity = loadQuantity,
            LoadAverageCost = loadAverageCost,
            LoadValue = loadValue,
            UnloadQuantity = unloadQuantity,
            UnloadAveragePrice = unloadAveragePrice,
            UnloadValue = unloadValue,
            FinalQuantity = finalQuantity,
            FinalValue = finalQuantity * finalUnitValue
        };
    }

    private static ArticleAccountMovementItem? SelectMovement(
        IReadOnlyList<ArticleAccountMovementItem> movements,
        string? selectedKey)
    {
        if (movements.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(selectedKey))
        {
            var selected = movements.FirstOrDefault(movement => movement.Key == selectedKey);
            if (selected is not null)
            {
                return selected;
            }
        }

        return movements[0];
    }

    private static decimal Decimal(object? value) =>
        value is null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);

    private sealed record ArticleAccountArticle(
        string Code,
        string Description,
        decimal InitialQuantity,
        decimal StandardCost);
}
