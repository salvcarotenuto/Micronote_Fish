using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class GroupPurchaseStatsRepository(MicronoteDb database)
{
    public async Task<GroupPurchaseStatsPageModel> GetAsync(
        DateOnly dateFrom,
        DateOnly dateTo,
        string? grouping,
        bool loadData,
        CancellationToken cancellationToken = default)
    {
        var mode = NormalizeGrouping(grouping);
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var stores = await ListStoresAsync(connection, cancellationToken);
        if (!loadData)
        {
            return new GroupPurchaseStatsPageModel
            {
                DateFrom = dateFrom,
                DateTo = dateTo,
                Grouping = mode,
                Stores = stores.Select(store => new GroupPurchaseStatsStore(store.Code, store.Name, 0)).ToArray()
            };
        }

        var revenues = await LoadRevenuesAsync(connection, dateFrom, dateTo, cancellationToken);
        var rows = await LoadRowsAsync(connection, dateFrom, dateTo, mode, stores, revenues.ByStore, cancellationToken);
        return new GroupPurchaseStatsPageModel
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            Grouping = mode,
            IsLoaded = true,
            TotalRevenue = revenues.Total,
            Stores = stores.Select(store => new GroupPurchaseStatsStore(
                store.Code,
                store.Name,
                revenues.ByStore.GetValueOrDefault(store.Code))).ToArray(),
            Rows = rows
        };
    }

    public async Task<GroupPurchaseStatsDetails> GetDetailsAsync(
        int code,
        DateOnly dateFrom,
        DateOnly dateTo,
        string? grouping,
        CancellationToken cancellationToken = default)
    {
        if (code <= 0)
        {
            return new GroupPurchaseStatsDetails([], []);
        }

        var column = GroupColumn(NormalizeGrouping(grouping));
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var articles = new List<GroupPurchaseStatsArticle>();
        var articleSql = $"""
            SELECT COALESCE(A.Codice, '') AS Codice,
                   COALESCE(A.Descrizione, '') AS Descrizione,
                   COALESCE(SUM(M.Importo), 0) AS Importo
            FROM Articoli A
            INNER JOIN Movimenti M ON M.Articolo = A.Codice
            WHERE A.{column} = @code
              AND M.TipoMov = 'C'
              AND M.DataMov BETWEEN @dateFrom AND @dateTo
            GROUP BY A.Codice, A.Descrizione
            ORDER BY A.Codice;
            """;
        await using (var command = new MySqlCommand(articleSql, connection))
        {
            AddParameters(command, dateFrom, dateTo);
            command.Parameters.AddWithValue("@code", code);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                articles.Add(new GroupPurchaseStatsArticle(
                    Convert.ToString(reader["Codice"]) ?? "",
                    Convert.ToString(reader["Descrizione"]) ?? "",
                    Decimal(reader["Importo"])));
            }
        }

        var suppliers = new List<GroupPurchaseStatsSupplier>();
        var supplierSql = $"""
            SELECT COALESCE(M.Ditta, 0) AS Codice,
                   COALESCE(F.Nome, '') AS Nome,
                   COALESCE(SUM(M.Importo), 0) AS Importo
            FROM Movimenti M
            LEFT JOIN Fornitori F ON F.Codice = M.Ditta
            LEFT JOIN Articoli A ON A.Codice = M.Articolo
            WHERE A.{column} = @code
              AND M.TipoMov = 'C'
              AND M.DataMov BETWEEN @dateFrom AND @dateTo
            GROUP BY M.Ditta, F.Nome
            ORDER BY M.Ditta, F.Nome;
            """;
        await using (var command = new MySqlCommand(supplierSql, connection))
        {
            AddParameters(command, dateFrom, dateTo);
            command.Parameters.AddWithValue("@code", code);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                suppliers.Add(new GroupPurchaseStatsSupplier(
                    Convert.ToInt32(reader["Codice"]),
                    Convert.ToString(reader["Nome"]) ?? "",
                    Decimal(reader["Importo"])));
            }
        }

        return new GroupPurchaseStatsDetails(articles, suppliers);
    }

    private static async Task<IReadOnlyList<GroupPurchaseStatsRow>> LoadRowsAsync(
        MySqlConnection connection,
        DateOnly dateFrom,
        DateOnly dateTo,
        string grouping,
        IReadOnlyList<(int Code, string Name)> stores,
        IReadOnlyDictionary<int, decimal> revenues,
        CancellationToken cancellationToken)
    {
        var column = GroupColumn(grouping);
        var table = GroupTable(grouping);
        var sql = $"""
            SELECT COALESCE(A.{column}, 0) AS Codice,
                   COALESCE(G.Descrizione, '') AS Descrizione,
                   COALESCE(M.PuntoV, 0) AS PuntoV,
                   COALESCE(SUM(M.Importo), 0) AS Importo
            FROM Movimenti M
            LEFT JOIN Articoli A ON A.Codice = M.Articolo
            LEFT JOIN {table} G ON G.Codice = A.{column}
            WHERE M.TipoMov = 'C'
              AND M.DataMov BETWEEN @dateFrom AND @dateTo
            GROUP BY A.{column}, G.Descrizione, M.PuntoV
            ORDER BY A.{column}, G.Descrizione, M.PuntoV;
            """;
        await using var command = new MySqlCommand(sql, connection);
        AddParameters(command, dateFrom, dateTo);
        var baseRows = new List<(int Code, string Description, int StoreCode, decimal Amount)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                baseRows.Add((
                    Convert.ToInt32(reader["Codice"]),
                    Convert.ToString(reader["Descrizione"]) ?? "",
                    Convert.ToInt32(reader["PuntoV"]),
                    Decimal(reader["Importo"])));
            }
        }

        return baseRows
            .GroupBy(row => new { row.Code, row.Description })
            .Select(group =>
            {
                var total = group.Sum(row => row.Amount);
                var amounts = group
                    .GroupBy(row => row.StoreCode)
                    .ToDictionary(rows => rows.Key, rows => rows.Sum(row => row.Amount));
                var cells = stores.Select(store =>
                {
                    var amount = amounts.GetValueOrDefault(store.Code);
                    return new GroupPurchaseStatsCell(
                        store.Code,
                        amount,
                        total == 0 ? 0 : amount * 100 / total,
                        revenues.GetValueOrDefault(store.Code) == 0
                            ? 0
                            : amount * 100 / revenues.GetValueOrDefault(store.Code));
                }).ToArray();
                return new GroupPurchaseStatsRow(
                    group.Key.Code,
                    group.Key.Code == 0 ? "- - -" : group.Key.Description,
                    total,
                    cells);
            })
            .OrderBy(row => row.Code)
            .ThenBy(row => row.Description)
            .ToArray();
    }

    private static async Task<(decimal Total, Dictionary<int, decimal> ByStore)> LoadRevenuesAsync(
        MySqlConnection connection,
        DateOnly dateFrom,
        DateOnly dateTo,
        CancellationToken cancellationToken)
    {
        const string storesSql = """
            SELECT COALESCE(MI.PuntoV, 0) AS PuntoV,
                   COALESCE(SUM(MI.Imponibile + MI.NonImpo), 0) AS Ricavo
            FROM MovivaRg MI
            INNER JOIN Vendite V ON V.Anno = MI.Anno AND V.Codice = MI.Codice
            WHERE V.DataMov BETWEEN @dateFrom AND @dateTo
              AND MI.Settore = 20
            GROUP BY MI.PuntoV;
            """;
        await using var storesCommand = new MySqlCommand(storesSql, connection);
        AddParameters(storesCommand, dateFrom, dateTo);
        var values = new Dictionary<int, decimal>();
        await using var reader = await storesCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values[Convert.ToInt32(reader["PuntoV"])] = Decimal(reader["Ricavo"]);
        }

        return (values.Values.Sum(), values);
    }

    private static async Task<IReadOnlyList<(int Code, string Name)>> ListStoresAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT Codice, COALESCE(Nome, '') AS Nome FROM PuntiVendita ORDER BY Codice;";
        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<(int, string)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((Convert.ToInt32(reader["Codice"]), Convert.ToString(reader["Nome"]) ?? ""));
        }
        return rows;
    }

    private static string NormalizeGrouping(string? grouping) => grouping?.ToLowerInvariant() switch
    {
        "category" => "category",
        "subgroup" => "subgroup",
        _ => "group"
    };

    private static string GroupColumn(string grouping) => grouping switch
    {
        "category" => "Categoria",
        "subgroup" => "Sottogruppo",
        _ => "Gruppo"
    };

    private static string GroupTable(string grouping) => grouping switch
    {
        "category" => "Categorie",
        "subgroup" => "Sottogruppi",
        _ => "Gruppi"
    };

    private static void AddParameters(MySqlCommand command, DateOnly dateFrom, DateOnly dateTo)
    {
        command.Parameters.AddWithValue("@dateFrom", dateFrom.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@dateTo", dateTo.ToDateTime(TimeOnly.MinValue));
    }

    private static decimal Decimal(object? value) =>
        value is null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
}
