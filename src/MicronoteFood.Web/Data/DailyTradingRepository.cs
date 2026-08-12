using System.Globalization;
using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class DailyTradingRepository(MicronoteDb database)
{
    public async Task<DateOnly?> LastDateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand("SELECT MAX(DataMov) FROM Movimenti;", connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : DateOnly.FromDateTime(Convert.ToDateTime(value));
    }

    public async Task<DailyTradingPageModel> GetAsync(
        DateOnly date,
        string? grouping,
        CancellationToken cancellationToken = default)
    {
        grouping = NormalizeGrouping(grouping);
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var inventoryDate = await LoadInventoryDateAsync(connection, cancellationToken);
        var rows = await ListAsync(connection, date, grouping, inventoryDate, cancellationToken);
        var totals = await TotalsAsync(connection, date, cancellationToken);
        return new DailyTradingPageModel
        {
            MovementDate = date,
            Grouping = grouping,
            Rows = rows,
            Totals = totals
        };
    }

    private static string NormalizeGrouping(string? grouping) => grouping?.Trim().ToLowerInvariant() switch
    {
        "categoria" => "categoria",
        "gruppo" => "gruppo",
        "specie" => "specie",
        "provenienza" => "provenienza",
        _ => ""
    };

    private static async Task<IReadOnlyList<DailyTradingRow>> ListAsync(
        MySqlConnection connection,
        DateOnly date,
        string grouping,
        DateOnly? inventoryDate,
        CancellationToken cancellationToken)
    {
        var (codeExpression, descriptionExpression, classificationJoin, classificationFilter) = grouping switch
        {
            "categoria" => ("CAST(a.Categoria AS CHAR)", "COALESCE(cl.Descrizione, '')", "LEFT JOIN Categorie cl ON cl.Codice = a.Categoria", "AND COALESCE(a.Categoria, 0) <> 0"),
            "gruppo" => ("CAST(a.Gruppo AS CHAR)", "COALESCE(cl.Descrizione, '')", "LEFT JOIN Gruppi cl ON cl.Codice = a.Gruppo", "AND COALESCE(a.Gruppo, 0) <> 0"),
            "specie" => ("CAST(a.Specie AS CHAR)", "COALESCE(cl.Descrizione, '')", "LEFT JOIN Specie cl ON cl.Codice = a.Specie", "AND COALESCE(a.Specie, 0) <> 0"),
            "provenienza" => ("CAST(a.Provenienza AS CHAR)", "COALESCE(cl.Descrizione, '')", "LEFT JOIN Provenienza cl ON cl.Codice = a.Provenienza", "AND COALESCE(a.Provenienza, 0) <> 0"),
            _ => ("CAST(a.Codice AS CHAR)", "COALESCE(a.Descrizione, '')", "", "")
        };

        var sql = $$"""
            WITH purchased AS (
                SELECT rg.Articolo,
                       SUM(COALESCE(rg.Quantita, 0)) AS Quantita,
                       SUM(COALESCE(rg.Importo, 0)) AS Importo
                FROM CaricoRg rg
                INNER JOIN Carico documento ON documento.ID = rg.ID
                WHERE documento.DataDoc = @date
                GROUP BY rg.Articolo
            ),
            sold AS (
                SELECT rg.Articolo,
                       SUM(COALESCE(rg.Quantita, 0)) AS Quantita,
                       SUM(COALESCE(rg.Importo, 0)) AS Importo
                FROM VenditeRg rg
                INNER JOIN Vendite documento ON documento.ID = rg.ID
                WHERE documento.DataDoc = @date
                GROUP BY rg.Articolo
            ),
            daily_articles AS (
                SELECT Articolo FROM purchased
                UNION
                SELECT Articolo FROM sold
            ),
            stock AS (
                SELECT Articolo,
                       SUM(CASE WHEN TipoMov = 'C' THEN COALESCE(Quantita, 0) ELSE 0 END)
                       - SUM(CASE WHEN TipoMov = 'S' THEN COALESCE(Quantita, 0) ELSE 0 END) AS Movimento
                FROM Movimenti
                WHERE @hasInventory = 1
                  AND DataMov BETWEEN @inventoryDate AND @date
                GROUP BY Articolo
            )
            SELECT {{codeExpression}} AS Codice,
                   {{descriptionExpression}} AS Descrizione,
                   SUM(COALESCE(p.Quantita, 0)) AS QtaAcquisti,
                   SUM(COALESCE(p.Importo, 0)) AS ValAcquisti,
                   SUM(COALESCE(v.Quantita, 0)) AS QtaVendite,
                   SUM(COALESCE(v.Importo, 0)) AS ValVendite,
                   SUM((CASE WHEN @hasInventory = 1 THEN COALESCE(a.GiacinP, 0) ELSE 0 END)
                       + COALESCE(s.Movimento, 0)) AS Rimanenza
            FROM daily_articles d
            INNER JOIN Articoli a ON a.Codice = d.Articolo
            {{classificationJoin}}
            LEFT JOIN purchased p ON p.Articolo = a.Codice
            LEFT JOIN sold v ON v.Articolo = a.Codice
            LEFT JOIN stock s ON s.Articolo = a.Codice
            WHERE 1 = 1 {{classificationFilter}}
            GROUP BY {{codeExpression}}, {{descriptionExpression}}
            ORDER BY {{descriptionExpression}}, {{codeExpression}};
            """;

        var hasInventory = inventoryDate.HasValue && inventoryDate.Value <= date;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@date", MySqlDbType.Date).Value = date.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@hasInventory", hasInventory ? 1 : 0);
        command.Parameters.Add("@inventoryDate", MySqlDbType.Date).Value =
            (inventoryDate ?? date).ToDateTime(TimeOnly.MinValue);
        var rows = new List<DailyTradingRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var purchasedQuantity = Decimal(reader["QtaAcquisti"]);
            var purchasedValue = Decimal(reader["ValAcquisti"]);
            var soldQuantity = Decimal(reader["QtaVendite"]);
            var soldValue = Decimal(reader["ValVendite"]);
            rows.Add(new DailyTradingRow(
                Convert.ToString(reader["Codice"]) ?? "",
                Convert.ToString(reader["Descrizione"]) ?? "",
                purchasedQuantity,
                purchasedValue,
                purchasedQuantity == 0 ? 0 : purchasedValue / purchasedQuantity,
                soldQuantity,
                soldValue,
                soldQuantity == 0 ? 0 : soldValue / soldQuantity,
                purchasedQuantity - soldQuantity,
                Decimal(reader["Rimanenza"])));
        }
        return rows;
    }

    private static async Task<DailyTradingTotals> TotalsAsync(
        MySqlConnection connection,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                (SELECT COALESCE(SUM(Totale), 0) FROM Carico WHERE DataDoc = @date) AS Acquisti,
                (SELECT COALESCE(SUM(Importo), 0) FROM MovCassa WHERE TipoMov = 'U' AND DataMov = @date) AS PagatoAcquisti,
                (SELECT COALESCE(SUM(Totale), 0) FROM Vendite WHERE DataDoc = @date) AS Vendite,
                (SELECT COALESCE(SUM(Importo), 0) FROM MovCassa WHERE TipoMov = 'E' AND DataMov = @date) AS PagatoVendite;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@date", MySqlDbType.Date).Value = date.ToDateTime(TimeOnly.MinValue);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new DailyTradingTotals
        {
            Purchases = Decimal(reader["Acquisti"]),
            PurchasesPaid = Decimal(reader["PagatoAcquisti"]),
            Sales = Decimal(reader["Vendite"]),
            SalesPaid = Decimal(reader["PagatoVendite"])
        };
    }

    private static async Task<DateOnly?> LoadInventoryDateAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COALESCE(Valore, '') FROM Opzioni WHERE Chiave = 'DataInventario' LIMIT 1;",
            connection);
        var value = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken))?.Trim();
        string[] formats = ["yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "yyyyMMdd"];
        if (DateOnly.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }
        return int.TryParse(value, out var year) && year is >= 1900 and <= 9999
            ? new DateOnly(year, 1, 1)
            : null;
    }

    private static decimal Decimal(object? value) => value is null or DBNull ? 0 : Convert.ToDecimal(value);
}
