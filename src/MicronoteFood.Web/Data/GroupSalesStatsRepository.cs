using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class GroupSalesStatsRepository(MicronoteDb database)
{
    public async Task<GroupSalesStatsPageModel> GetAsync(DateOnly dateFrom, DateOnly dateTo, string? grouping, bool loadData, CancellationToken cancellationToken = default)
    {
        var mode = NormalizeGrouping(grouping);
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var stores = await ListStoresAsync(connection, cancellationToken);
        var page = new GroupSalesStatsPageModel
        {
            DateFrom = dateFrom, DateTo = dateTo, Grouping = mode,
            Stores = stores.Select(store => new GroupSalesStatsStore(store.Code, store.Name)).ToArray()
        };
        if (!loadData) return page;
        page.IsLoaded = true;
        page.Rows = await LoadRowsAsync(connection, dateFrom, dateTo, mode, stores, cancellationToken);
        return page;
    }

    public async Task<GroupSalesStatsDetails> GetDetailsAsync(int code, DateOnly dateFrom, DateOnly dateTo, string? grouping, CancellationToken cancellationToken = default)
    {
        if (code <= 0) return new GroupSalesStatsDetails([], []);
        var column = GroupColumn(NormalizeGrouping(grouping));
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var articles = new List<GroupSalesStatsArticle>();
        var articlesSql = $"""
            SELECT COALESCE(A.Codice, '') AS Codice, COALESCE(A.Descrizione, '') AS Descrizione, COALESCE(SUM(VR.Importo), 0) AS Importo
            FROM Articoli A INNER JOIN VenditeRg VR ON VR.Articolo = A.Codice INNER JOIN Vendite V ON V.ID = VR.ID
            WHERE A.{column} = @code AND V.DataDoc BETWEEN @dateFrom AND @dateTo
            GROUP BY A.Codice, A.Descrizione ORDER BY A.Codice;
            """;
        await using (var command = new MySqlCommand(articlesSql, connection))
        {
            AddParameters(command, dateFrom, dateTo); command.Parameters.AddWithValue("@code", code);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) articles.Add(new(Convert.ToString(reader["Codice"]) ?? "", Convert.ToString(reader["Descrizione"]) ?? "", Decimal(reader["Importo"])));
        }
        var customers = new List<GroupSalesStatsCustomer>();
        var customersSql = $"""
            SELECT COALESCE(V.Cliente, 0) AS Codice, COALESCE(C.Nome, '') AS Nome, COALESCE(SUM(VR.Importo), 0) AS Importo
            FROM VenditeRg VR INNER JOIN Vendite V ON V.ID = VR.ID LEFT JOIN Clienti C ON C.Codice = V.Cliente
            LEFT JOIN Articoli A ON A.Codice = VR.Articolo
            WHERE A.{column} = @code AND V.DataDoc BETWEEN @dateFrom AND @dateTo
            GROUP BY V.Cliente, C.Nome ORDER BY V.Cliente, C.Nome;
            """;
        await using (var command = new MySqlCommand(customersSql, connection))
        {
            AddParameters(command, dateFrom, dateTo); command.Parameters.AddWithValue("@code", code);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) customers.Add(new(Convert.ToInt32(reader["Codice"]), Convert.ToString(reader["Nome"]) ?? "", Decimal(reader["Importo"])));
        }
        return new GroupSalesStatsDetails(articles, customers);
    }

    private static async Task<IReadOnlyList<GroupSalesStatsRow>> LoadRowsAsync(MySqlConnection connection, DateOnly dateFrom, DateOnly dateTo, string grouping, IReadOnlyList<(int Code, string Name)> stores, CancellationToken cancellationToken)
    {
        var column = GroupColumn(grouping); var table = GroupTable(grouping);
        var sql = $"""
            SELECT COALESCE(A.{column}, 0) AS Codice, COALESCE(G.Descrizione, '') AS Descrizione, COALESCE(V.PuntoV, 0) AS PuntoV, COALESCE(SUM(VR.Importo), 0) AS Importo
            FROM VenditeRg VR INNER JOIN Vendite V ON V.ID = VR.ID LEFT JOIN Articoli A ON A.Codice = VR.Articolo LEFT JOIN {table} G ON G.Codice = A.{column}
            WHERE V.DataDoc BETWEEN @dateFrom AND @dateTo
            GROUP BY A.{column}, G.Descrizione, V.PuntoV ORDER BY A.{column}, G.Descrizione, V.PuntoV;
            """;
        await using var command = new MySqlCommand(sql, connection); AddParameters(command, dateFrom, dateTo);
        var baseRows = new List<(int Code, string Description, int StoreCode, decimal Amount)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) baseRows.Add((Convert.ToInt32(reader["Codice"]), Convert.ToString(reader["Descrizione"]) ?? "", Convert.ToInt32(reader["PuntoV"]), Decimal(reader["Importo"])));
        return baseRows.GroupBy(row => new { row.Code, row.Description }).Select(group =>
        {
            var total = group.Sum(row => row.Amount); var amounts = group.GroupBy(row => row.StoreCode).ToDictionary(rows => rows.Key, rows => rows.Sum(row => row.Amount));
            return new GroupSalesStatsRow(group.Key.Code, group.Key.Code == 0 ? "- - -" : group.Key.Description, total,
                stores.Select(store => { var amount = amounts.GetValueOrDefault(store.Code); return new GroupSalesStatsCell(store.Code, amount, total == 0 ? 0 : amount * 100 / total); }).ToArray());
        }).OrderBy(row => row.Code).ThenBy(row => row.Description).ToArray();
    }

    private static async Task<IReadOnlyList<(int Code, string Name)>> ListStoresAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand("SELECT Codice, COALESCE(Nome, '') AS Nome FROM PuntiVendita ORDER BY Codice;", connection);
        var rows = new List<(int, string)>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) rows.Add((Convert.ToInt32(reader["Codice"]), Convert.ToString(reader["Nome"]) ?? "")); return rows;
    }
    private static string NormalizeGrouping(string? grouping) => grouping?.ToLowerInvariant() switch { "group" => "group", "subgroup" => "subgroup", "species" => "species", _ => "category" };
    private static string GroupColumn(string grouping) => grouping switch { "category" => "Categoria", "subgroup" => "Sottogruppo", "species" => "Specie", _ => "Gruppo" };
    private static string GroupTable(string grouping) => grouping switch { "category" => "Categorie", "subgroup" => "Sottogruppi", "species" => "Specie", _ => "Gruppi" };
    private static void AddParameters(MySqlCommand command, DateOnly dateFrom, DateOnly dateTo) { command.Parameters.AddWithValue("@dateFrom", dateFrom.ToDateTime(TimeOnly.MinValue)); command.Parameters.AddWithValue("@dateTo", dateTo.ToDateTime(TimeOnly.MinValue)); }
    private static decimal Decimal(object? value) => value is null or DBNull ? 0 : Convert.ToDecimal(value);
}
