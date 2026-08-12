using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class WarehouseStatisticsRepository(MicronoteDb database)
{
    public async Task<WarehouseStatisticsPageModel> GetAsync(int year, int month, string? grouping, bool loadData, CancellationToken cancellationToken = default)
    {
        var mode = NormalizeGrouping(grouping);
        var page = new WarehouseStatisticsPageModel { Year = year, Month = month, Grouping = mode };
        if (!loadData) return page;
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        page.Rows = await LoadRowsAsync(connection, year, month, mode, cancellationToken);
        page.IsLoaded = true;
        return page;
    }

    public async Task<IReadOnlyList<WarehouseStatisticsCustomer>> GetCustomersAsync(string code, int year, int month, string? grouping, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return [];
        var mode = NormalizeGrouping(grouping);
        var (groupExpression, _, _) = Grouping(mode);
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var sql = $"""
            SELECT CAST(V.Cliente AS CHAR) AS Codice, COALESCE(C.Nome, '') AS Nome,
                   COALESCE(SUM(VR.Quantita), 0) AS Quantita, COALESCE(SUM(VR.Importo), 0) AS Importo
            FROM VenditeRg VR
            INNER JOIN Vendite V ON V.ID = VR.ID
            LEFT JOIN Clienti C ON C.Codice = V.Cliente
            LEFT JOIN Articoli A ON A.Codice = VR.Articolo
            WHERE YEAR(V.DataDoc) = @year
              AND (@month = 0 OR MONTH(V.DataDoc) = @month)
              AND CAST({groupExpression} AS CHAR) = @code
            GROUP BY V.Cliente, C.Nome
            ORDER BY Quantita DESC, V.Cliente;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year); command.Parameters.AddWithValue("@month", month); command.Parameters.AddWithValue("@code", code);
        var rows = new List<WarehouseStatisticsCustomer>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) rows.Add(new(Convert.ToString(reader["Codice"]) ?? "", Convert.ToString(reader["Nome"]) ?? "", Decimal(reader["Quantita"]), Decimal(reader["Importo"])));
        return rows;
    }

    private static async Task<IReadOnlyList<WarehouseStatisticsRow>> LoadRowsAsync(MySqlConnection connection, int year, int month, string grouping, CancellationToken cancellationToken)
    {
        var (expression, description, join) = Grouping(grouping);
        var sql = $"""
            WITH Acquisti AS (
                SELECT {expression} AS Codice, {description} AS Descrizione,
                       COALESCE(SUM(CR.Quantita), 0) AS Quantita, COALESCE(SUM(CR.Importo), 0) AS Importo
                FROM CaricoRg CR INNER JOIN Carico C ON C.ID = CR.ID LEFT JOIN Articoli A ON A.Codice = CR.Articolo {join}
                WHERE YEAR(C.DataDoc) = @year AND (@month = 0 OR MONTH(C.DataDoc) = @month)
                GROUP BY {expression}, {description}
            ), Vendite AS (
                SELECT {expression} AS Codice, {description} AS Descrizione,
                       COALESCE(SUM(VR.Quantita), 0) AS Quantita, COALESCE(SUM(VR.Importo), 0) AS Importo
                FROM VenditeRg VR INNER JOIN Vendite V ON V.ID = VR.ID LEFT JOIN Articoli A ON A.Codice = VR.Articolo {join}
                WHERE YEAR(V.DataDoc) = @year AND (@month = 0 OR MONTH(V.DataDoc) = @month)
                GROUP BY {expression}, {description}
            )
            SELECT COALESCE(Acquisti.Codice, Vendite.Codice) AS Codice,
                   COALESCE(NULLIF(Acquisti.Descrizione, ''), Vendite.Descrizione, '') AS Descrizione,
                   COALESCE(Acquisti.Quantita, 0) AS QtaAcquisti, COALESCE(Acquisti.Importo, 0) AS ImportoAcquisti,
                   COALESCE(Vendite.Quantita, 0) AS QtaVendite, COALESCE(Vendite.Importo, 0) AS ImportoVendite
            FROM Acquisti LEFT JOIN Vendite ON Vendite.Codice = Acquisti.Codice
            UNION ALL
            SELECT Vendite.Codice, Vendite.Descrizione, 0, 0, Vendite.Quantita, Vendite.Importo
            FROM Vendite LEFT JOIN Acquisti ON Acquisti.Codice = Vendite.Codice WHERE Acquisti.Codice IS NULL
            ORDER BY ImportoVendite DESC, Codice;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year); command.Parameters.AddWithValue("@month", month);
        var rows = new List<WarehouseStatisticsRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var purchaseQuantity = Decimal(reader["QtaAcquisti"]); var purchaseAmount = Decimal(reader["ImportoAcquisti"]);
            var soldQuantity = Decimal(reader["QtaVendite"]); var soldAmount = Decimal(reader["ImportoVendite"]);
            var cost = purchaseQuantity == 0 ? 0 : purchaseAmount / purchaseQuantity;
            var price = soldQuantity == 0 ? 0 : soldAmount / soldQuantity;
            var revenue = soldAmount - purchaseAmount;
            rows.Add(new(Convert.ToString(reader["Codice"]) ?? "", Convert.ToString(reader["Descrizione"]) ?? "", purchaseQuantity, purchaseAmount, cost, soldQuantity, soldAmount, purchaseQuantity - soldQuantity, price, revenue, cost == 0 ? 0 : (price - cost) * 100 / cost));
        }
        return rows;
    }

    private static (string Expression, string Description, string Join) Grouping(string mode) => mode switch
    {
        "category" => ("COALESCE(A.Categoria, 0)", "COALESCE(G.Descrizione, '')", "LEFT JOIN Categorie G ON G.Codice = A.Categoria"),
        "species" => ("COALESCE(A.Specie, 0)", "COALESCE(G.Descrizione, '')", "LEFT JOIN Specie G ON G.Codice = A.Specie"),
        "origin" => ("COALESCE(A.Provenienza, 0)", "COALESCE(G.Descrizione, '')", "LEFT JOIN Provenienza G ON G.Codice = A.Provenienza"),
        _ => ("A.Codice", "COALESCE(A.Descrizione, '')", "")
    };
    private static string NormalizeGrouping(string? grouping) => grouping?.ToLowerInvariant() switch { "category" => "category", "species" => "species", "origin" => "origin", _ => "article" };
    private static decimal Decimal(object? value) => value is null or DBNull ? 0 : Convert.ToDecimal(value);
}
