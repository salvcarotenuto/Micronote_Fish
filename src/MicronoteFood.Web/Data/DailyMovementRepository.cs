using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class DailyMovementRepository(MicronoteDb database)
{
    public async Task<DateOnly?> LastDateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand("SELECT MAX(DataMov) FROM Movimenti;", connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : DateOnly.FromDateTime(Convert.ToDateTime(value));
    }

    public async Task<DailyMovementPageModel> GetAsync(
        DateOnly dateFrom,
        DateOnly dateTo,
        string? selectionType,
        string? articleCode,
        int? classificationCode,
        CancellationToken cancellationToken = default)
    {
        selectionType = NormalizeType(selectionType);
        articleCode = articleCode?.Trim() ?? "";
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var articles = await LoadArticlesAsync(connection, cancellationToken);
        var categories = await LoadClassificationsAsync(connection, "categoria", cancellationToken);
        var groups = await LoadClassificationsAsync(connection, "gruppo", cancellationToken);
        var species = await LoadClassificationsAsync(connection, "specie", cancellationToken);
        var origins = await LoadClassificationsAsync(connection, "provenienza", cancellationToken);
        var selectedArticle = articles.FirstOrDefault(row => row.Code == articleCode);
        var canLoad = selectionType == "articolo" ? articleCode.Length > 0 : classificationCode.HasValue;
        var rows = canLoad
            ? await LoadRowsAsync(connection, dateFrom, dateTo, selectionType, articleCode, classificationCode, cancellationToken)
            : [];
        var purchases = rows.Where(row => row.MovementType == "C").ToArray();
        var sales = rows.Where(row => row.MovementType == "S").ToArray();
        return new DailyMovementPageModel
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            SelectionType = selectionType,
            ArticleCode = articleCode,
            ArticleDescription = selectedArticle?.Description ?? "",
            ClassificationCode = classificationCode,
            Articles = articles,
            Categories = categories,
            Groups = groups,
            Species = species,
            Origins = origins,
            Rows = rows,
            Totals = new DailyMovementTotals
            {
                PurchasedPackages = purchases.Sum(row => row.Packages),
                PurchasedQuantity = purchases.Sum(row => row.Quantity),
                PurchasedAmount = purchases.Sum(row => row.Amount),
                SoldPackages = sales.Sum(row => row.Packages),
                SoldQuantity = sales.Sum(row => row.Quantity),
                SoldAmount = sales.Sum(row => row.Amount)
            }
        };
    }

    private static string NormalizeType(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "categoria" => "categoria",
        "gruppo" => "gruppo",
        "specie" => "specie",
        "provenienza" => "provenienza",
        _ => "articolo"
    };

    private static async Task<IReadOnlyList<DailyMovementArticleOption>> LoadArticlesAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT Codice, COALESCE(Descrizione, '') AS Descrizione FROM Articoli ORDER BY Descrizione, Codice;",
            connection);
        var rows = new List<DailyMovementArticleOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new DailyMovementArticleOption(
                Convert.ToString(reader["Codice"]) ?? "",
                Convert.ToString(reader["Descrizione"]) ?? ""));
        }
        return rows;
    }

    private static async Task<IReadOnlyList<LookupOption>> LoadClassificationsAsync(
        MySqlConnection connection,
        string selectionType,
        CancellationToken cancellationToken)
    {
        var table = selectionType switch
        {
            "categoria" => "Categorie",
            "gruppo" => "Gruppi",
            "specie" => "Specie",
            _ => "Provenienza"
        };
        await using var command = new MySqlCommand(
            $"SELECT Codice, COALESCE(Descrizione, '') AS Descrizione FROM {table} ORDER BY Descrizione, Codice;",
            connection);
        var rows = new List<LookupOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new LookupOption(Convert.ToInt32(reader["Codice"]), Convert.ToString(reader["Descrizione"]) ?? ""));
        }
        return rows;
    }

    private static async Task<IReadOnlyList<DailyMovementRow>> LoadRowsAsync(
        MySqlConnection connection,
        DateOnly dateFrom,
        DateOnly dateTo,
        string selectionType,
        string articleCode,
        int? classificationCode,
        CancellationToken cancellationToken)
    {
        var classificationColumn = selectionType switch
        {
            "categoria" => "a.Categoria",
            "gruppo" => "a.Gruppo",
            "specie" => "a.Specie",
            "provenienza" => "a.Provenienza",
            _ => "NULL"
        };
        var selectionSql = selectionType == "articolo"
            ? "m.Articolo = @articleCode"
            : $"{classificationColumn} = @classificationCode";
        var sql = $$"""
            SELECT m.Codice,
                   COALESCE(m.TipoMov, '') AS TipoMov,
                   COALESCE(cm.Descrizione, '') AS Causale,
                   COALESCE(m.Articolo, '') AS Articolo,
                   COALESCE(a.Descrizione, '') AS Descrizione,
                   COALESCE(m.Ditta, 0) AS Ditta,
                   CASE
                       WHEN m.CliFor = 'C' THEN COALESCE(c.Nome, '')
                       WHEN m.CliFor = 'F' THEN COALESCE(f.Nome, '')
                       ELSE ''
                   END AS Nome,
                   COALESCE(m.Colli, 0) AS Colli,
                   COALESCE(m.Quantita, 0) AS Quantita,
                   COALESCE(m.Prezzo, 0) AS Prezzo,
                   COALESCE(m.Importo, 0) AS Importo
            FROM Movimenti m
            LEFT JOIN Articoli a ON a.Codice = m.Articolo
            LEFT JOIN Clienti c ON c.Codice = m.Ditta AND m.CliFor = 'C'
            LEFT JOIN Fornitori f ON f.Codice = m.Ditta AND m.CliFor = 'F'
            LEFT JOIN CausaliMag cm ON cm.Codice = m.Causale
            WHERE {{selectionSql}}
              AND m.DataMov BETWEEN @dateFrom AND @dateTo
              AND m.TipoMov IN ('C', 'S')
            ORDER BY m.TipoMov, m.Codice, m.Riga;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@dateFrom", MySqlDbType.Date).Value = dateFrom.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@dateTo", MySqlDbType.Date).Value = dateTo.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@articleCode", articleCode);
        command.Parameters.AddWithValue("@classificationCode", classificationCode ?? 0);
        var rows = new List<DailyMovementRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new DailyMovementRow(
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["TipoMov"]) ?? "",
                Convert.ToString(reader["Causale"]) ?? "",
                Convert.ToString(reader["Articolo"]) ?? "",
                Convert.ToString(reader["Descrizione"]) ?? "",
                Convert.ToInt32(reader["Ditta"]),
                Convert.ToString(reader["Nome"]) ?? "",
                Convert.ToInt32(reader["Colli"]),
                Decimal(reader["Quantita"]),
                Decimal(reader["Prezzo"]),
                Decimal(reader["Importo"])));
        }
        return rows;
    }

    private static decimal Decimal(object? value) => value is null or DBNull ? 0 : Convert.ToDecimal(value);
}
