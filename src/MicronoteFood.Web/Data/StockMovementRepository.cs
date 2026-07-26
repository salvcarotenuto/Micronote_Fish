using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class StockMovementRepository(MicronoteDb database)
{
    public async Task<StockMovementListPageModel> GetListAsync(
        int year,
        DateOnly dateFrom,
        DateOnly dateTo,
        string? articleCode,
        int? categoryCode,
        int? groupCode,
        int? subgroupCode,
        bool includeLoads,
        bool includeUnloads,
        int? customerCode,
        int? supplierCode,
        string? selectedKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedArticle = (articleCode ?? "").Trim();
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var movements = await ListMovementsAsync(
            connection,
            dateFrom,
            dateTo,
            normalizedArticle,
            categoryCode,
            groupCode,
            subgroupCode,
            MovementFilter(includeLoads, includeUnloads),
            customerCode,
            supplierCode,
            cancellationToken);
        var selected = SelectMovement(movements, selectedKey);

        return new StockMovementListPageModel
        {
            Year = year,
            DateFrom = dateFrom,
            DateTo = dateTo,
            ArticleCode = normalizedArticle,
            ArticleDescription = await ArticleDescriptionAsync(connection, normalizedArticle, cancellationToken),
            CategoryCode = categoryCode,
            GroupCode = groupCode,
            SubgroupCode = subgroupCode,
            IncludeLoads = includeLoads,
            IncludeUnloads = includeUnloads,
            CustomerCode = customerCode,
            CustomerName = await SubjectNameAsync(connection, "Clienti", customerCode, cancellationToken),
            SupplierCode = supplierCode,
            SupplierName = await SubjectNameAsync(connection, "Fornitori", supplierCode, cancellationToken),
            SelectedKey = selected?.Key,
            Movements = movements,
            Totals = await LoadTotalsAsync(
                connection,
                dateFrom,
                dateTo,
                normalizedArticle,
                categoryCode,
                groupCode,
                subgroupCode,
                cancellationToken),
            Categories = await ListLookupAsync(connection, "categorie", cancellationToken),
            Groups = await ListLookupAsync(connection, "gruppi", cancellationToken),
            Subgroups = await ListLookupAsync(connection, "sottogruppi", cancellationToken),
            Customers = await ListCustomersAsync(connection, cancellationToken),
            Suppliers = await ListSuppliersAsync(connection, cancellationToken)
        };
    }

    private static async Task<IReadOnlyList<StockMovementListItem>> ListMovementsAsync(
        MySqlConnection connection,
        DateOnly dateFrom,
        DateOnly dateTo,
        string articleCode,
        int? categoryCode,
        int? groupCode,
        int? subgroupCode,
        string movementFilter,
        int? customerCode,
        int? supplierCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT M.Anno,
                   M.Settore,
                   M.Codice,
                   M.Riga,
                   COALESCE(M.Causale, 0) AS Causale,
                   COALESCE(CausaliMag.Descrizione, '') AS CausaleDescrizione,
                   M.DataMov,
                   COALESCE(M.NumDoc, '') AS NumDoc,
                   COALESCE(M.Articolo, '') AS Articolo,
                   COALESCE(Articoli.Descrizione, '') AS ArticoloDescrizione,
                   COALESCE(M.TipoMov, '') AS TipoMov,
                   COALESCE(Articoli.Uma, '') AS Ums,
                   COALESCE(M.Quantita, 0) AS Quantita,
                   COALESCE(M.Prezzo, 0) AS Prezzo,
                   COALESCE(M.Importo, 0) AS Importo,
                   COALESCE(CaricoRg.AliqIva, Articoli.AliqIva, 0) AS AliqIva,
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
            LEFT JOIN Articoli ON Articoli.Codice = M.Articolo
            LEFT JOIN CausaliMag ON CausaliMag.Codice = M.Causale
            LEFT JOIN CaricoRg ON CaricoRg.Anno = M.Anno
                AND CaricoRg.Codice = M.Codice
                AND CaricoRg.Riga = M.Riga
            LEFT JOIN Carico ON Carico.Anno = M.Anno
                AND Carico.Codice = M.Codice
                AND M.Settore = 10
            LEFT JOIN Vendite ON Vendite.Anno = M.Anno
                AND Vendite.Codice = M.Codice
                AND M.Settore = 30
            WHERE M.DataMov BETWEEN @dateFrom AND @dateTo
              AND (@articleCode = '' OR M.Articolo = @articleCode)
              AND (@categoryCode IS NULL OR Articoli.Categoria = @categoryCode)
              AND (@groupCode IS NULL OR Articoli.Gruppo = @groupCode)
              AND (@subgroupCode IS NULL OR Articoli.Sottogruppo = @subgroupCode)
              AND (@movementFilter = '' OR M.TipoMov = @movementFilter)
              AND (@movementFilter <> 'X')
              AND (@customerCode IS NULL OR (M.CliFor = 'C' AND M.Ditta = @customerCode))
              AND (@supplierCode IS NULL OR (M.CliFor = 'F' AND M.Ditta = @supplierCode))
            ORDER BY M.DataMov, M.Settore, M.Codice, M.Riga;
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddFilterParameters(command, dateFrom, dateTo, articleCode, categoryCode, groupCode, subgroupCode);
        command.Parameters.AddWithValue("@movementFilter", movementFilter);
        command.Parameters.AddWithValue("@customerCode", customerCode is null ? DBNull.Value : customerCode.Value);
        command.Parameters.AddWithValue("@supplierCode", supplierCode is null ? DBNull.Value : supplierCode.Value);

        var rows = new List<StockMovementListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new StockMovementListItem(
                Convert.ToInt32(reader["Anno"]),
                Convert.ToInt32(reader["Settore"]),
                Convert.ToInt32(reader["Codice"]),
                Convert.ToInt32(reader["Riga"]),
                Convert.ToInt32(reader["Causale"]),
                Convert.ToString(reader["CausaleDescrizione"]) ?? "",
                DateOnly.FromDateTime(Convert.ToDateTime(reader["DataMov"])),
                Convert.ToString(reader["NumDoc"]) ?? "",
                Convert.ToString(reader["Articolo"]) ?? "",
                Convert.ToString(reader["ArticoloDescrizione"]) ?? "",
                Convert.ToString(reader["TipoMov"]) ?? "",
                Convert.ToString(reader["Ums"]) ?? "",
                Decimal(reader["Quantita"]),
                Decimal(reader["Prezzo"]),
                Decimal(reader["Importo"]),
                Decimal(reader["AliqIva"]),
                Convert.ToString(reader["CliFor"]) ?? "",
                Convert.ToInt32(reader["Ditta"]),
                Convert.ToString(reader["Nome"]) ?? "",
                Convert.ToInt32(reader["StockLoadId"]),
                Convert.ToInt32(reader["SaleId"])));
        }

        return rows;
    }

    private static async Task<StockMovementTotals> LoadTotalsAsync(
        MySqlConnection connection,
        DateOnly dateFrom,
        DateOnly dateTo,
        string articleCode,
        int? categoryCode,
        int? groupCode,
        int? subgroupCode,
        CancellationToken cancellationToken)
    {
        var initialQuantity = await ScalarDecimalAsync(
            connection,
            """
            SELECT COALESCE(SUM(Articoli.GiacIn), 0)
            FROM Articoli
            WHERE (@articleCode = '' OR Articoli.Codice = @articleCode)
              AND (@categoryCode IS NULL OR Articoli.Categoria = @categoryCode)
              AND (@groupCode IS NULL OR Articoli.Gruppo = @groupCode)
              AND (@subgroupCode IS NULL OR Articoli.Sottogruppo = @subgroupCode);
            """,
            dateFrom,
            dateTo,
            articleCode,
            categoryCode,
            groupCode,
            subgroupCode,
            cancellationToken);
        var load = await MovementTotalAsync(
            connection,
            "C",
            dateFrom,
            dateTo,
            articleCode,
            categoryCode,
            groupCode,
            subgroupCode,
            cancellationToken);
        var unload = await MovementTotalAsync(
            connection,
            "S",
            dateFrom,
            dateTo,
            articleCode,
            categoryCode,
            groupCode,
            subgroupCode,
            cancellationToken);
        var averageCost = load.Quantity == 0 ? 0 : load.Value / load.Quantity;
        var stockQuantity = initialQuantity + load.Quantity - unload.Quantity;

        return new StockMovementTotals
        {
            InitialQuantity = initialQuantity,
            InitialValue = initialQuantity * averageCost,
            LoadQuantity = load.Quantity,
            LoadValue = load.Value,
            UnloadQuantity = unload.Quantity,
            UnloadValue = unload.Value,
            StockQuantity = stockQuantity,
            StockValue = stockQuantity * averageCost
        };
    }

    private static async Task<(decimal Quantity, decimal Value)> MovementTotalAsync(
        MySqlConnection connection,
        string movementType,
        DateOnly dateFrom,
        DateOnly dateTo,
        string articleCode,
        int? categoryCode,
        int? groupCode,
        int? subgroupCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(SUM(Movimenti.Quantita), 0) AS Quantita,
                   COALESCE(SUM(Movimenti.Importo), 0) AS Valore
            FROM Movimenti
            LEFT JOIN Articoli ON Articoli.Codice = Movimenti.Articolo
            WHERE Movimenti.TipoMov = @movementType
              AND Movimenti.DataMov BETWEEN @dateFrom AND @dateTo
              AND (@articleCode = '' OR Movimenti.Articolo = @articleCode)
              AND (@categoryCode IS NULL OR Articoli.Categoria = @categoryCode)
              AND (@groupCode IS NULL OR Articoli.Gruppo = @groupCode)
              AND (@subgroupCode IS NULL OR Articoli.Sottogruppo = @subgroupCode);
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddFilterParameters(command, dateFrom, dateTo, articleCode, categoryCode, groupCode, subgroupCode);
        command.Parameters.AddWithValue("@movementType", movementType);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (0, 0);
        }

        return (Decimal(reader["Quantita"]), Decimal(reader["Valore"]));
    }

    private static async Task<decimal> ScalarDecimalAsync(
        MySqlConnection connection,
        string sql,
        DateOnly dateFrom,
        DateOnly dateTo,
        string articleCode,
        int? categoryCode,
        int? groupCode,
        int? subgroupCode,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(sql, connection);
        AddFilterParameters(command, dateFrom, dateTo, articleCode, categoryCode, groupCode, subgroupCode);
        return Decimal(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<IReadOnlyList<LookupOption>> ListLookupAsync(
        MySqlConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var sql = table switch
        {
            "categorie" => "SELECT Codice, COALESCE(Descrizione, '') AS Descrizione FROM Categorie ORDER BY Descrizione, Codice;",
            "gruppi" => "SELECT Codice, COALESCE(Descrizione, '') AS Descrizione FROM Gruppi ORDER BY Descrizione, Codice;",
            "sottogruppi" => "SELECT Codice, COALESCE(Descrizione, '') AS Descrizione FROM Sottogruppi ORDER BY Descrizione, Codice;",
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };

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

    private static async Task<IReadOnlyList<StockMovementSubjectOption>> ListCustomersAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Clienti.Codice,
                   COALESCE(Clienti.Nome, '') AS Nome
            FROM Clienti
            LEFT JOIN Movimenti ON Movimenti.CliFor = 'C'
                AND Movimenti.Ditta = Clienti.Codice
            WHERE Movimenti.TipoMov = 'S'
              AND Movimenti.Ditta = Clienti.Codice
            GROUP BY Clienti.Codice, Clienti.Nome
            ORDER BY Clienti.Nome;
            """;

        return await ListSubjectsAsync(connection, sql, cancellationToken);
    }

    private static async Task<IReadOnlyList<StockMovementSubjectOption>> ListSuppliersAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Fornitori.Codice,
                   COALESCE(Fornitori.Nome, '') AS Nome
            FROM Fornitori
            LEFT JOIN Movimenti ON Movimenti.CliFor = 'F'
                AND Movimenti.Ditta = Fornitori.Codice
            WHERE Movimenti.TipoMov = 'C'
              AND Movimenti.Ditta = Fornitori.Codice
            GROUP BY Fornitori.Codice, Fornitori.Nome
            ORDER BY Fornitori.Nome;
            """;

        return await ListSubjectsAsync(connection, sql, cancellationToken);
    }

    private static async Task<IReadOnlyList<StockMovementSubjectOption>> ListSubjectsAsync(
        MySqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<StockMovementSubjectOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new StockMovementSubjectOption(
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Nome"]) ?? ""));
        }

        return rows;
    }

    private static async Task<string> ArticleDescriptionAsync(
        MySqlConnection connection,
        string articleCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(articleCode))
        {
            return "";
        }

        await using var command = new MySqlCommand(
            "SELECT COALESCE(Descrizione, '') FROM Articoli WHERE Codice = @code LIMIT 1;",
            connection);
        command.Parameters.AddWithValue("@code", articleCode);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? "";
    }

    private static async Task<string> SubjectNameAsync(
        MySqlConnection connection,
        string table,
        int? code,
        CancellationToken cancellationToken)
    {
        if (code is null or <= 0)
        {
            return "";
        }

        var sql = $"SELECT COALESCE(Nome, '') FROM {table} WHERE Codice = @code LIMIT 1;";
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", code.Value);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? "";
    }

    private static void AddFilterParameters(
        MySqlCommand command,
        DateOnly dateFrom,
        DateOnly dateTo,
        string articleCode,
        int? categoryCode,
        int? groupCode,
        int? subgroupCode)
    {
        command.Parameters.AddWithValue("@dateFrom", dateFrom.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@dateTo", dateTo.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@articleCode", articleCode);
        command.Parameters.AddWithValue("@categoryCode", categoryCode is null ? DBNull.Value : categoryCode.Value);
        command.Parameters.AddWithValue("@groupCode", groupCode is null ? DBNull.Value : groupCode.Value);
        command.Parameters.AddWithValue("@subgroupCode", subgroupCode is null ? DBNull.Value : subgroupCode.Value);
    }

    private static string MovementFilter(bool includeLoads, bool includeUnloads)
    {
        if (includeLoads && includeUnloads)
        {
            return "";
        }

        if (includeLoads)
        {
            return "C";
        }

        return includeUnloads ? "S" : "X";
    }

    private static StockMovementListItem? SelectMovement(
        IReadOnlyList<StockMovementListItem> movements,
        string? selectedKey) =>
        string.IsNullOrWhiteSpace(selectedKey)
            ? movements.FirstOrDefault()
            : movements.FirstOrDefault(row => row.Key == selectedKey)
              ?? movements.FirstOrDefault();

    private static decimal Decimal(object? value) =>
        value is null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
}
