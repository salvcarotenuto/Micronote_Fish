using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class StockUnloadRepository(
    MicronoteDb database,
    ProgressiveCodeService progressiveCodes)
{
    private const int Sector = 11;

    public async Task<StockUnloadListModel> GetListAsync(int year, int? month, int? cause, int? store, string? selectedKey, CancellationToken ct)
    {
        await using var connection = await database.OpenConnectionAsync(ct);
        const string sql = """
            SELECT m.ID, m.Anno, m.Codice, m.DataMov, m.Causale, m.Articolo,
                   COALESCE(a.Descrizione, '') Descrizione, COALESCE(m.Quantita, 0) Quantita,
                   COALESCE(m.PuntoV, 0) PuntoV, COALESCE(p.Nome, '') PuntoVendita,
                   COALESCE(m.Ditta, 0) Ditta, COALESCE(f.Nome, '') Fornitore
            FROM Movimenti m
            LEFT JOIN Articoli a ON a.Codice=m.Articolo
            LEFT JOIN PuntiVendita p ON p.Codice=m.PuntoV
            LEFT JOIN Fornitori f ON f.Codice=m.Ditta
            WHERE m.Anno=@year AND m.Settore=11
              AND (@month IS NULL OR MONTH(m.DataMov)=@month)
              AND (@cause IS NULL OR m.Causale=@cause)
              AND (@store IS NULL OR m.PuntoV=@store)
            ORDER BY m.DataMov DESC, m.Codice DESC;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@month", month is null ? DBNull.Value : month);
        command.Parameters.AddWithValue("@cause", cause is null ? DBNull.Value : cause);
        command.Parameters.AddWithValue("@store", store is null ? DBNull.Value : store);
        var rows = new List<StockUnloadListItem>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var causeCode = Convert.ToInt32(reader["Causale"]);
                rows.Add(new(
                    Convert.ToInt32(reader["ID"]), Convert.ToInt32(reader["Anno"]), Convert.ToInt32(reader["Codice"]),
                    DateOnly.FromDateTime(Convert.ToDateTime(reader["DataMov"])), causeCode,
                    causeCode == 15 ? "Reso a fornitore" : "Scarico per perdite",
                    Convert.ToString(reader["Articolo"]) ?? "", Convert.ToString(reader["Descrizione"]) ?? "",
                    Convert.ToDecimal(reader["Quantita"]), Convert.ToInt32(reader["PuntoV"]),
                    Convert.ToString(reader["PuntoVendita"]) ?? "", Convert.ToInt32(reader["Ditta"]),
                    Convert.ToString(reader["Fornitore"]) ?? ""));
            }
        }

        var years = new SortedSet<int>(rows.Select(x => x.Year)) { year };
        await using (var yearsCommand = new MySqlCommand("SELECT DISTINCT Anno FROM Movimenti WHERE Settore=11 ORDER BY Anno DESC;", connection))
        await using (var reader = await yearsCommand.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) years.Add(Convert.ToInt32(reader[0]));

        var stores = await LoadStoresAsync(connection, ct);
        var selected = rows.FirstOrDefault(x => x.Key == selectedKey) ?? rows.FirstOrDefault();
        return new() { Year=year, Month=month, CauseCode=cause, StoreCode=store, Rows=rows,
            Years=years.OrderDescending().ToArray(), Stores=stores, SelectedKey=selected?.Key ?? "" };
    }

    public async Task<(StockUnloadEditModel Document, StockUnloadMaskModel Mask)?> GetEditAsync(int id, CancellationToken ct)
    {
        await using var connection = await database.OpenConnectionAsync(ct);
        const string sql = """
            SELECT m.*, COALESCE(a.Descrizione,'') Descrizione, COALESCE(a.Uma,'') Ums,
                   COALESCE(f.Nome,'') FornitoreNome
            FROM Movimenti m LEFT JOIN Articoli a ON a.Codice=m.Articolo
            LEFT JOIN Fornitori f ON f.Codice=m.Ditta
            WHERE m.ID=@id AND m.Settore=11 LIMIT 1;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var article = Convert.ToString(reader["Articolo"]) ?? "";
        var document = new StockUnloadEditModel {
            Id=id, Year=Convert.ToInt32(reader["Anno"]), Code=Convert.ToInt32(reader["Codice"]), CauseCode=Convert.ToInt32(reader["Causale"]),
            Date=DateOnly.FromDateTime(Convert.ToDateTime(reader["DataMov"])), ArticleCode=article,
            ArticleDescription=Convert.ToString(reader["Descrizione"]) ?? "",
            UnitMeasure=Convert.ToString(reader["Ums"]) ?? "", Quantity=Convert.ToDecimal(reader["Quantita"]),
            StoreCode=Convert.ToInt32(reader["PuntoV"]), SupplierCode=Convert.ToInt32(reader["Ditta"]),
            SupplierName=Convert.ToString(reader["FornitoreNome"]) ?? ""
        };
        await reader.DisposeAsync();
        document.Stock = await StockAsync(connection, article, document.Id, ct);
        return (document, await LoadMaskAsync(connection, ct));
    }

    public async Task<StockUnloadMaskModel> GetMaskAsync(CancellationToken ct)
    {
        await using var connection = await database.OpenConnectionAsync(ct);
        return await LoadMaskAsync(connection, ct);
    }

    public Task<int> NextCodeAsync(int year, CancellationToken ct) =>
        progressiveCodes.NextCodeAsync(
            "Movimenti",
            "Codice",
            new Dictionary<string, object?> { ["Anno"] = year },
            cancellationToken: ct);

    public async Task<decimal> GetStockAsync(string articleCode, int id, CancellationToken ct)
    {
        await using var connection = await database.OpenConnectionAsync(ct);
        return await StockAsync(connection, articleCode.Trim(), id, ct);
    }

    public async Task<(bool Success, string Error, int Id, int Code)> SaveAsync(StockUnloadEditModel model, CancellationToken ct)
    {
        if (model.CauseCode is not (14 or 15)) return (false, "Selezionare il tipo di movimento.", model.Id, model.Code);
        if (string.IsNullOrWhiteSpace(model.ArticleCode)) return (false, "Selezionare l'articolo.", model.Id, model.Code);
        if (model.Quantity <= 0) return (false, "La quantità deve essere maggiore di zero.", model.Id, model.Code);
        if (model.Date.Year != model.Year) return (false, "La data deve appartenere all'anno della partita.", model.Id, model.Code);
        if (model.CauseCode == 15 && model.SupplierCode <= 0) return (false, "Selezionare il fornitore per il reso.", model.Id, model.Code);

        await using var connection = await database.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        var code = model.Code;
        if (model.IsNew)
        {
            code = await progressiveCodes.NextCodeAsync(
                connection,
                "Movimenti",
                "Codice",
                new Dictionary<string, object?> { ["Anno"] = model.Year },
                transaction: tx,
                cancellationToken: ct);
        }
        if (!model.IsNew)
        {
            const string updateSql = """
                UPDATE Movimenti
                SET Causale=@cause, DataMov=@date, TipoMov='S', Articolo=@article,
                    CliFor=@subject, Ditta=@supplier, Quantita=@quantity, PuntoV=@store
                WHERE ID=@id AND Settore=11;
                """;
            await using var update = new MySqlCommand(updateSql, connection, tx);
            AddSaveParameters(update, model, code);
            update.Parameters.AddWithValue("@id", model.Id);
            if (await update.ExecuteNonQueryAsync(ct) != 1)
                return (false, "Movimento di scarico non trovato.", model.Id, code);
            await tx.CommitAsync(ct);
            return (true, "", model.Id, code);
        }

        const string insertSql = """
            INSERT INTO Movimenti
              (Anno,Settore,Codice,Riga,Causale,DataMov,TipoMov,Articolo,CliFor,Ditta,Quantita,PuntoV)
            VALUES (@year,11,@code,1,@cause,@date,'S',@article,@subject,@supplier,@quantity,@store);
            """;
        await using var insert = new MySqlCommand(insertSql, connection, tx);
        AddSaveParameters(insert, model, code);
        await insert.ExecuteNonQueryAsync(ct);
        var id = Convert.ToInt32(insert.LastInsertedId);
        await tx.CommitAsync(ct);
        return (true, "", id, code);
    }

    private static void AddSaveParameters(MySqlCommand insert, StockUnloadEditModel model, int code)
    {
        insert.Parameters.AddWithValue("@year", model.Year);
        insert.Parameters.AddWithValue("@code", code);
        insert.Parameters.AddWithValue("@cause", model.CauseCode);
        insert.Parameters.AddWithValue("@date", model.Date.ToDateTime(TimeOnly.MinValue));
        insert.Parameters.AddWithValue("@article", model.ArticleCode.Trim());
        insert.Parameters.AddWithValue("@subject", model.CauseCode == 15 ? "F" : " ");
        insert.Parameters.AddWithValue("@supplier", model.CauseCode == 15 ? model.SupplierCode : 0);
        insert.Parameters.AddWithValue("@quantity", model.Quantity);
        insert.Parameters.AddWithValue("@store", model.StoreCode);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        await using var connection = await database.OpenConnectionAsync(ct);
        await using var command = new MySqlCommand("DELETE FROM Movimenti WHERE ID=@id AND Settore=11;", connection);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<decimal> StockAsync(MySqlConnection c, string article, int excludeId, CancellationToken ct)
    {
        const string sql = """
          SELECT COALESCE(a.GiacinP,0)+COALESCE(SUM(CASE WHEN m.TipoMov='C' THEN m.Quantita WHEN m.TipoMov='S' THEN -m.Quantita ELSE 0 END),0)
          FROM Articoli a LEFT JOIN Movimenti m ON m.Articolo=a.Codice
            AND m.ID<>@id
          WHERE a.Codice=@article GROUP BY a.Codice,a.GiacinP;
          """;
        await using var command = new MySqlCommand(sql, c);
        command.Parameters.AddWithValue("@id", excludeId);
        command.Parameters.AddWithValue("@article", article);
        return Convert.ToDecimal(await command.ExecuteScalarAsync(ct) ?? 0);
    }

    private static async Task<StockUnloadMaskModel> LoadMaskAsync(MySqlConnection c, CancellationToken ct) => new()
    {
        Stores = await LoadStoresAsync(c, ct),
        Articles = await ReadArticlesAsync(c, ct),
        Suppliers = await ReadSuppliersAsync(c, ct)
    };

    private static async Task<IReadOnlyList<LookupOption>> LoadStoresAsync(MySqlConnection c, CancellationToken ct)
    {
        var result = new List<LookupOption>();
        await using var command = new MySqlCommand("SELECT Codice,COALESCE(Nome,'') Nome FROM PuntiVendita ORDER BY Nome;", c);
        await using var r = await command.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) result.Add(new(Convert.ToInt32(r[0]), Convert.ToString(r[1]) ?? ""));
        return result;
    }
    private static async Task<IReadOnlyList<StockUnloadArticle>> ReadArticlesAsync(MySqlConnection c, CancellationToken ct)
    {
        var result = new List<StockUnloadArticle>();
        await using var command = new MySqlCommand("SELECT Codice,COALESCE(Descrizione,''),COALESCE(Uma,'') FROM Articoli ORDER BY Descrizione;", c);
        await using var r = await command.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) result.Add(new(Convert.ToString(r[0]) ?? "", Convert.ToString(r[1]) ?? "", Convert.ToString(r[2]) ?? ""));
        return result;
    }
    private static async Task<IReadOnlyList<StockUnloadSupplier>> ReadSuppliersAsync(MySqlConnection c, CancellationToken ct)
    {
        var result = new List<StockUnloadSupplier>();
        await using var command = new MySqlCommand("SELECT Codice,COALESCE(Nome,'') FROM Fornitori ORDER BY Nome;", c);
        await using var r = await command.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) result.Add(new(Convert.ToInt32(r[0]), Convert.ToString(r[1]) ?? ""));
        return result;
    }
}
