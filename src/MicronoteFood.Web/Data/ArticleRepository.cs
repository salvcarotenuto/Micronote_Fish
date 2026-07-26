using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class ArticleRepository(MicronoteDb database)
{
    public async Task<IReadOnlyList<ArticleListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                a.Codice,
                COALESCE(a.Descrizione, '') AS Descrizione,
                COALESCE(a.Ums, '') AS Ums,
                a.Categoria,
                COALESCE(ct.Descrizione, '') AS CategoriaDescrizione,
                a.Gruppo,
                COALESCE(gr.Descrizione, '') AS GruppoDescrizione,
                a.Sottogruppo,
                COALESCE(sg.Descrizione, '') AS SottogruppoDescrizione,
                COALESCE(a.CostoStd, 0) AS CostoStd,
                COALESCE(a.AliqIva, 0) AS AliqIva,
                a.Fornitore,
                COALESCE(fn.Nome, '') AS FornitoreNome
            FROM Articoli a
            LEFT JOIN Categorie ct ON ct.Codice = a.Categoria
            LEFT JOIN Gruppi gr ON gr.Codice = a.Gruppo
            LEFT JOIN Sottogruppi sg ON sg.Codice = a.Sottogruppo
            LEFT JOIN fornitori fn ON fn.Codice = a.Fornitore
            ORDER BY a.Descrizione, a.Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var articles = new List<ArticleListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            articles.Add(new ArticleListItem(
                Text(reader, "Codice") ?? "",
                Text(reader, "Descrizione") ?? "",
                Text(reader, "Ums") ?? "",
                Integer(reader, "Categoria"),
                Text(reader, "CategoriaDescrizione") ?? "",
                Integer(reader, "Gruppo"),
                Text(reader, "GruppoDescrizione") ?? "",
                Integer(reader, "Sottogruppo"),
                Text(reader, "SottogruppoDescrizione") ?? "",
                Decimal(reader, "CostoStd") ?? 0,
                Decimal(reader, "AliqIva") ?? 0,
                Integer(reader, "Fornitore"),
                Text(reader, "FornitoreNome") ?? ""));
        }

        return articles;
    }

    public async Task<ArticleEditModel?> GetAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                a.*,
                COALESCE(fn.Nome, '') AS FornitoreNome
            FROM Articoli a
            LEFT JOIN fornitori fn ON fn.Codice = a.Fornitore
            WHERE a.Codice = @code
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

        var standardCost = Decimal(reader, "CostoStd");
        return new ArticleEditModel
        {
            Code = Text(reader, "Codice") ?? "",
            Description = Text(reader, "Descrizione") ?? "",
            UnitMeasureCode = Text(reader, "Ums"),
            CategoryCode = Integer(reader, "Categoria"),
            GroupCode = Integer(reader, "Gruppo"),
            SubgroupCode = Integer(reader, "Sottogruppo"),
            VatRate = Decimal(reader, "AliqIva"),
            NetWeight = Decimal(reader, "PesoNt"),
            Pieces = Integer(reader, "Pezzi"),
            StandardCost = standardCost,
            StandardPrice = Decimal(reader, "PrezzoStd"),
            InitialStock = Decimal(reader, "GiacIn"),
            DailyConsumption = Decimal(reader, "Consumo"),
            SupplierCode = Integer(reader, "Fornitore"),
            SupplierName = Text(reader, "FornitoreNome") ?? "",
            SupplierArticleCode = Text(reader, "CodiceFn"),
            AverageCost = standardCost,
            LastCost = standardCost
        };
    }

    public async Task<ArticleLookups> GetLookupsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        return new ArticleLookups(
            await LoadTextLookupAsync(
                connection,
                "SELECT Codice, Descrizione FROM UMisura ORDER BY Descrizione, Codice",
                cancellationToken),
            await LoadLookupAsync(
                connection,
                "SELECT Codice, Descrizione FROM Categorie ORDER BY Descrizione, Codice",
                cancellationToken),
            await LoadLookupAsync(
                connection,
                "SELECT Codice, Descrizione FROM Gruppi ORDER BY Descrizione, Codice",
                cancellationToken),
            await LoadLookupAsync(
                connection,
                "SELECT Codice, Descrizione FROM Sottogruppi ORDER BY Descrizione, Codice",
                cancellationToken),
            await LoadLookupAsync(
                connection,
                "SELECT Codice, Nome AS Descrizione FROM fornitori ORDER BY Nome, Codice",
                cancellationToken));
    }

    public async Task<bool> ExistsAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM Articoli WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code.Trim());
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task<string> InsertAsync(
        ArticleEditModel article,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO Articoli
                (Codice, Descrizione, Ums, Categoria, Gruppo, Sottogruppo, AliqIva,
                 PesoNt, Pezzi, CostoStd, PrezzoStd, GiacIn, Consumo, Fornitore, CodiceFn)
            VALUES
                (@code, @description, @unitMeasure, @category, @group, @subgroup, @vatRate,
                 @netWeight, @pieces, @standardCost, @standardPrice, @initialStock,
                 @dailyConsumption, @supplier, @supplierArticleCode);
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, article);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return article.Code.Trim();
    }

    public async Task<bool> UpdateAsync(
        ArticleEditModel article,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        const string sql = """
            UPDATE Articoli
            SET
                Descrizione = @description,
                Ums = @unitMeasure,
                Categoria = @category,
                Gruppo = @group,
                Sottogruppo = @subgroup,
                AliqIva = @vatRate,
                PesoNt = @netWeight,
                Pezzi = @pieces,
                CostoStd = @standardCost,
                PrezzoStd = @standardPrice,
                GiacIn = @initialStock,
                Consumo = @dailyConsumption,
                Fornitore = @supplier,
                CodiceFn = @supplierArticleCode
            WHERE Codice = @code;
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, article);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<ArticleDeleteResult> DeleteAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "DELETE FROM Articoli WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1
                ? new ArticleDeleteResult(true, "Articolo eliminato.")
                : new ArticleDeleteResult(false, "Articolo non trovato.");
        }
        catch (MySqlException)
        {
            return new ArticleDeleteResult(
                false,
                "L'articolo non può essere eliminato: esistono movimenti collegati.");
        }
    }

    private static async Task<IReadOnlyList<LookupOption>> LoadLookupAsync(
        MySqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<LookupOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new LookupOption(
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Descrizione"]) ?? ""));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<TextLookupOption>> LoadTextLookupAsync(
        MySqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<TextLookupOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new TextLookupOption(
                Convert.ToString(reader["Codice"]) ?? "",
                Convert.ToString(reader["Descrizione"]) ?? ""));
        }

        return rows;
    }

    private static void AddSaveParameters(MySqlCommand command, ArticleEditModel article)
    {
        command.Parameters.AddWithValue("@code", article.Code.Trim());
        command.Parameters.AddWithValue("@description", article.Description.Trim());
        command.Parameters.AddWithValue("@unitMeasure", DbText(article.UnitMeasureCode));
        command.Parameters.AddWithValue("@category", DbInt(article.CategoryCode));
        command.Parameters.AddWithValue("@group", DbInt(article.GroupCode));
        command.Parameters.AddWithValue("@subgroup", DbInt(article.SubgroupCode));
        command.Parameters.AddWithValue("@vatRate", article.VatRate ?? 0);
        command.Parameters.AddWithValue("@netWeight", article.NetWeight ?? 0);
        command.Parameters.AddWithValue("@pieces", article.Pieces ?? 0);
        command.Parameters.AddWithValue("@standardCost", article.StandardCost ?? 0);
        command.Parameters.AddWithValue("@standardPrice", article.StandardPrice ?? 0);
        command.Parameters.AddWithValue("@initialStock", article.InitialStock ?? 0);
        command.Parameters.AddWithValue("@dailyConsumption", article.DailyConsumption ?? 0);
        command.Parameters.AddWithValue("@supplier", article.SupplierCode ?? 0);
        command.Parameters.AddWithValue("@supplierArticleCode", DbText(article.SupplierArticleCode));
    }

    private static object DbText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static object DbInt(int? value) =>
        value is null or 0 ? DBNull.Value : value.Value;

    private static string? Text(MySqlDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name))
            ? null
            : Convert.ToString(reader[name]);

    private static int? Integer(MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = Convert.ToInt32(reader.GetValue(ordinal));
        return value == 0 ? null : value;
    }

    private static decimal? Decimal(MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDecimal(reader.GetValue(ordinal));
    }
}
