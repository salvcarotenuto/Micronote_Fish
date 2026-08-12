using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class StockPurchaseStatsRepository(MicronoteDb database)
{
    public async Task<StockPurchaseStatsPageModel> GetInitialAsync(
        DateOnly dateFrom,
        DateOnly dateTo,
        int? storeCode,
        string? search,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return new StockPurchaseStatsPageModel
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            StoreCode = storeCode,
            Search = (search ?? "").Trim(),
            Stores = await ListStoresAsync(connection, cancellationToken)
        };
    }

    public async Task<StockPurchaseStatsPageModel> GetAsync(
        DateOnly dateFrom,
        DateOnly dateTo,
        int? storeCode,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = (search ?? "").Trim();
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var totalAmount = await TotalAmountAsync(connection, dateFrom, dateTo, storeCode, cancellationToken);
        var articles = await ListArticlesAsync(
            connection,
            dateFrom,
            dateTo,
            storeCode,
            normalizedSearch,
            totalAmount,
            cancellationToken);

        return new StockPurchaseStatsPageModel
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            StoreCode = storeCode,
            Search = normalizedSearch,
            TotalAmount = totalAmount,
            IsLoaded = true,
            Articles = articles,
            Stores = await ListStoresAsync(connection, cancellationToken)
        };
    }

    public async Task<IReadOnlyList<StockPurchaseStatsSupplierItem>> ListSuppliersAsync(
        string articleCode,
        DateOnly dateFrom,
        DateOnly dateTo,
        int? storeCode,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return await ListSuppliersAsync(connection, articleCode, dateFrom, dateTo, storeCode, cancellationToken);
    }

    private static async Task<IReadOnlyList<StockPurchaseStatsArticleItem>> ListArticlesAsync(
        MySqlConnection connection,
        DateOnly dateFrom,
        DateOnly dateTo,
        int? storeCode,
        string search,
        decimal totalAmount,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT M.Articolo,
                   COALESCE(A.Descrizione, '') AS Descrizione,
                   COALESCE(A.Uma, '') AS Ums,
                   COALESCE(SUM(M.Quantita), 0) AS Quantita,
                   COALESCE(SUM(M.Importo), 0) AS Importo,
                   COALESCE(A.CostoStd, 0) AS CostoStd,
                   MAX(M.DataMov) AS UltimaData
            FROM Movimenti M
            LEFT JOIN Articoli A ON A.Codice = M.Articolo
            WHERE M.TipoMov = 'C'
              AND M.DataMov BETWEEN @dateFrom AND @dateTo
              AND (@storeCode IS NULL OR M.PuntoV = @storeCode)
              AND (@search = '' OR A.Descrizione LIKE CONCAT('%', @search, '%') OR M.Articolo LIKE CONCAT('%', @search, '%'))
            GROUP BY M.Articolo, A.Descrizione, A.Uma, A.CostoStd
            ORDER BY M.Articolo;
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddFilterParameters(command, dateFrom, dateTo, storeCode);
        command.Parameters.AddWithValue("@search", search);

        var baseRows = new List<(
            string ArticleCode,
            string Description,
            string UnitMeasure,
            decimal Quantity,
            decimal Amount,
            decimal StandardCost,
            DateOnly? LastPurchaseDate)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                baseRows.Add((
                    Convert.ToString(reader["Articolo"]) ?? "",
                    Convert.ToString(reader["Descrizione"]) ?? "",
                    Convert.ToString(reader["Ums"]) ?? "",
                    Decimal(reader["Quantita"]),
                    Decimal(reader["Importo"]),
                    Decimal(reader["CostoStd"]),
                    Date(reader["UltimaData"])));
            }
        }

        var rows = new List<StockPurchaseStatsArticleItem>();
        foreach (var row in baseRows)
        {
            var prices = await LastPricesAsync(
                connection,
                row.ArticleCode,
                dateFrom,
                dateTo,
                storeCode,
                cancellationToken);

            rows.Add(new StockPurchaseStatsArticleItem(
                row.ArticleCode,
                row.Description,
                row.UnitMeasure,
                row.Quantity,
                row.Amount,
                row.StandardCost,
                row.Quantity == 0 ? 0 : row.Amount / row.Quantity,
                prices.ElementAtOrDefault(2).Price,
                prices.ElementAtOrDefault(1).Price,
                prices.ElementAtOrDefault(0).Price,
                row.LastPurchaseDate,
                totalAmount == 0 ? 0 : row.Amount * 100 / totalAmount));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<StockPurchaseStatsSupplierItem>> ListSuppliersAsync(
        MySqlConnection connection,
        string articleCode,
        DateOnly dateFrom,
        DateOnly dateTo,
        int? storeCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(M.Ditta, 0) AS Ditta,
                   COALESCE(F.Nome, '') AS Nome,
                   COALESCE(SUM(M.Quantita), 0) AS Quantita,
                   COALESCE(SUM(M.Importo), 0) AS Importo,
                   MAX(M.DataMov) AS UltimaData
            FROM Movimenti M
            LEFT JOIN Fornitori F ON F.Codice = M.Ditta
            WHERE M.TipoMov = 'C'
              AND M.Articolo = @articleCode
              AND M.DataMov BETWEEN @dateFrom AND @dateTo
              AND (@storeCode IS NULL OR M.PuntoV = @storeCode)
            GROUP BY M.Ditta, F.Nome
            ORDER BY Quantita DESC, F.Nome;
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddFilterParameters(command, dateFrom, dateTo, storeCode);
        command.Parameters.AddWithValue("@articleCode", articleCode);

        var baseRows = new List<(
            int SupplierCode,
            string SupplierName,
            decimal Quantity,
            decimal Amount,
            DateOnly? LastPurchaseDate)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                baseRows.Add((
                    Convert.ToInt32(reader["Ditta"]),
                    Convert.ToString(reader["Nome"]) ?? "",
                    Decimal(reader["Quantita"]),
                    Decimal(reader["Importo"]),
                    Date(reader["UltimaData"])));
            }
        }

        var rows = new List<StockPurchaseStatsSupplierItem>();
        foreach (var row in baseRows)
        {
            var prices = await LastPricesAsync(
                connection,
                articleCode,
                dateFrom,
                dateTo,
                storeCode,
                cancellationToken,
                row.SupplierCode);

            rows.Add(new StockPurchaseStatsSupplierItem(
                row.SupplierCode,
                row.SupplierName,
                row.Quantity,
                row.Amount,
                row.Quantity == 0 ? 0 : row.Amount / row.Quantity,
                prices.ElementAtOrDefault(0).Price,
                row.LastPurchaseDate));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<(decimal Price, DateOnly? Date)>> LastPricesAsync(
        MySqlConnection connection,
        string articleCode,
        DateOnly dateFrom,
        DateOnly dateTo,
        int? storeCode,
        CancellationToken cancellationToken,
        int? supplierCode = null)
    {
        const string sql = """
            SELECT COALESCE(Prezzo, 0) AS Prezzo, DataMov
            FROM Movimenti
            WHERE TipoMov = 'C'
              AND Articolo = @articleCode
              AND DataMov BETWEEN @dateFrom AND @dateTo
              AND (@storeCode IS NULL OR PuntoV = @storeCode)
              AND (@supplierCode IS NULL OR Ditta = @supplierCode)
            ORDER BY DataMov DESC, Anno DESC, Codice DESC, Riga DESC
            LIMIT 3;
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddFilterParameters(command, dateFrom, dateTo, storeCode);
        command.Parameters.AddWithValue("@articleCode", articleCode);
        command.Parameters.AddWithValue("@supplierCode", supplierCode is null ? DBNull.Value : supplierCode.Value);

        var rows = new List<(decimal Price, DateOnly? Date)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((Decimal(reader["Prezzo"]), Date(reader["DataMov"])));
        }

        return rows;
    }

    private static async Task<decimal> TotalAmountAsync(
        MySqlConnection connection,
        DateOnly dateFrom,
        DateOnly dateTo,
        int? storeCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(SUM(Importo), 0)
            FROM Movimenti
            WHERE TipoMov = 'C'
              AND DataMov BETWEEN @dateFrom AND @dateTo
              AND (@storeCode IS NULL OR PuntoV = @storeCode);
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddFilterParameters(command, dateFrom, dateTo, storeCode);
        return Decimal(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<IReadOnlyList<StockPurchaseStatsStoreOption>> ListStoresAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Codice, COALESCE(Nome, '') AS Nome
            FROM PuntiVendita
            ORDER BY Nome, Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<StockPurchaseStatsStoreOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new StockPurchaseStatsStoreOption(
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Nome"]) ?? ""));
        }

        return rows;
    }

    private static void AddFilterParameters(
        MySqlCommand command,
        DateOnly dateFrom,
        DateOnly dateTo,
        int? storeCode)
    {
        command.Parameters.AddWithValue("@dateFrom", dateFrom.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@dateTo", dateTo.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@storeCode", storeCode is null or <= 0 ? DBNull.Value : storeCode.Value);
    }

    private static decimal Decimal(object? value) =>
        value is null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);

    private static DateOnly? Date(object? value) =>
        value is null || value == DBNull.Value ? null : DateOnly.FromDateTime(Convert.ToDateTime(value));
}
