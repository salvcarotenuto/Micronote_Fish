using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class CounterSaleRepository(MicronoteDb database)
{
    public async Task<CounterSalePageData> LoadAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return new CounterSalePageData
        {
            Year = year,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Articles = await LoadArticlesAsync(connection, cancellationToken),
            Customers = await LoadCustomersAsync(connection, cancellationToken)
        };
    }

    public async Task<CounterSaleSaveResult> SaveAsync(
        int year,
        CounterSaleSaveModel sale,
        CancellationToken cancellationToken = default)
    {
        if (sale.CustomerCode <= 0 || sale.Rows.Count == 0)
            throw new InvalidOperationException("Selezionare il cliente e almeno un articolo.");

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var customer = await LoadCustomerAsync(
                connection, transaction, sale.CustomerCode, cancellationToken)
                ?? throw new InvalidOperationException("Cliente non trovato o non attivo.");
            var code = await NextCodeAsync(connection, transaction, year, cancellationToken);
            var merchandise = decimal.Round(sale.Rows.Sum(NetAmount), 2);
            var vat = decimal.Round(sale.Rows.Sum(row => row.Amount) - merchandise, 2);
            var gross = decimal.Round(sale.Rows.Sum(row => row.Amount), 2);
            var discount = decimal.Round(Math.Clamp(sale.Discount, 0, gross), 2);
            var total = gross - discount;

            await using var header = new MySqlCommand(
                """
                INSERT INTO Vendite
                    (Anno, Codice, Stato, NumDoc, DataDoc, Cliente, Merce,
                     Agente, Provvigione, Iva, Totale, Abbuono, PuntoV)
                VALUES
                    (@year, @code, 0, @code, @date, @customer, @merchandise,
                     0, 0, @vat, @total, @discount, @store);
                """, connection, transaction);
            header.Parameters.AddWithValue("@year", year);
            header.Parameters.AddWithValue("@code", code);
            header.Parameters.Add("@date", MySqlDbType.Date).Value = DateTime.Today;
            header.Parameters.AddWithValue("@customer", sale.CustomerCode);
            header.Parameters.AddWithValue("@merchandise", merchandise);
            header.Parameters.AddWithValue("@vat", vat);
            header.Parameters.AddWithValue("@total", total);
            header.Parameters.AddWithValue("@discount", discount);
            header.Parameters.AddWithValue("@store", customer);
            await header.ExecuteNonQueryAsync(cancellationToken);
            var id = checked((int)header.LastInsertedId);

            for (var index = 0; index < sale.Rows.Count; index++)
            {
                var row = sale.Rows[index];
                await ValidateArticleAsync(connection, transaction, row.ArticleCode, cancellationToken);
                await using var detail = new MySqlCommand(
                    """
                    INSERT INTO VenditeRg
                        (ID, Riga, Articolo, Ums, Colli, Tara, Quantita,
                         Prezzo, Iva, Importo)
                    VALUES
                        (@id, @row, @article, @unit, @packages, @tare, @quantity,
                         @price, @vat, @amount);
                    """, connection, transaction);
                detail.Parameters.AddWithValue("@id", id);
                detail.Parameters.AddWithValue("@row", index + 1);
                detail.Parameters.AddWithValue("@article", row.ArticleCode.Trim());
                detail.Parameters.AddWithValue("@unit", row.Unit.Trim());
                detail.Parameters.AddWithValue("@packages", Math.Max(0, row.Packages));
                detail.Parameters.AddWithValue("@tare", decimal.Round(row.Tare, 3));
                detail.Parameters.AddWithValue("@quantity", decimal.Round(row.Quantity, 3));
                detail.Parameters.AddWithValue("@price", decimal.Round(row.Price, 3));
                detail.Parameters.AddWithValue("@vat", decimal.Round(row.VatRate, 2));
                detail.Parameters.AddWithValue("@amount", decimal.Round(row.Amount, 2));
                await detail.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new CounterSaleSaveResult(id, code);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static decimal NetAmount(CounterSaleSaveRow row)
    {
        var quantity = row.Quantity != 0 ? row.Quantity : row.Packages;
        return decimal.Round(quantity * row.Price, 2);
    }

    private static async Task<IReadOnlyList<CounterSaleArticle>> LoadArticlesAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT a.Codice, COALESCE(a.Descrizione,'') Descrizione,
                   COALESCE(a.Umv,'') Ums,
                   COALESCE(a.Categoria,0) Categoria, COALESCE(c.Descrizione,'') CategoriaNome,
                   COALESCE(a.Gruppo,0) Gruppo, COALESCE(g.Descrizione,'') GruppoNome,
                   COALESCE(a.Specie,0) Specie, COALESCE(s.Descrizione,'') SpecieNome,
                   COALESCE(a.Provenienza,0) Provenienza, COALESCE(p.Descrizione,'') ProvenienzaNome,
                   COALESCE(a.Tara,0) Tara, COALESCE(a.GiacinP,0) Giacenza,
                   COALESCE(a.PrezzoStd,0) Prezzo, COALESCE(a.AliqIva,0) Iva
            FROM Articoli a
            LEFT JOIN Categorie c ON c.Codice=a.Categoria
            LEFT JOIN Gruppi g ON g.Codice=a.Gruppo
            LEFT JOIN Specie s ON s.Codice=a.Specie
            LEFT JOIN Provenienza p ON p.Codice=a.Provenienza
            ORDER BY a.Descrizione, a.Codice;
            """;
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<CounterSaleArticle>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CounterSaleArticle(
                Convert.ToString(reader["Codice"]) ?? "",
                Convert.ToString(reader["Descrizione"]) ?? "",
                Convert.ToString(reader["Ums"]) ?? "",
                Convert.ToInt32(reader["Categoria"]),
                Convert.ToString(reader["CategoriaNome"]) ?? "",
                Convert.ToInt32(reader["Gruppo"]),
                Convert.ToString(reader["GruppoNome"]) ?? "",
                Convert.ToInt32(reader["Specie"]),
                Convert.ToString(reader["SpecieNome"]) ?? "",
                Convert.ToInt32(reader["Provenienza"]),
                Convert.ToString(reader["ProvenienzaNome"]) ?? "",
                Convert.ToDecimal(reader["Tara"]),
                Convert.ToDecimal(reader["Giacenza"]),
                Convert.ToDecimal(reader["Prezzo"]),
                Convert.ToDecimal(reader["Iva"])));
        }
        return rows;
    }

    private static async Task<IReadOnlyList<CounterSaleCustomer>> LoadCustomersAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT Codice, COALESCE(Nome,'') Nome, COALESCE(PuntoV,0) PuntoV
            FROM Clienti WHERE COALESCE(Attivo,1)<>0 ORDER BY Nome, Codice;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<CounterSaleCustomer>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new(Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Nome"]) ?? "",
                Convert.ToInt32(reader["PuntoV"])));
        return rows;
    }

    private static async Task<int?> LoadCustomerAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int code,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COALESCE(PuntoV,0) FROM Clienti WHERE Codice=@code AND COALESCE(Attivo,1)<>0;",
            connection, transaction);
        command.Parameters.AddWithValue("@code", code);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? null : Convert.ToInt32(value);
    }

    private static async Task<int> NextCodeAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COALESCE(MAX(Codice),0)+1 FROM Vendite WHERE Anno=@year FOR UPDATE;",
            connection, transaction);
        command.Parameters.AddWithValue("@year", year);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task ValidateArticleAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string code,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM Articoli WHERE Codice=@code;",
            connection, transaction);
        command.Parameters.AddWithValue("@code", code.Trim());
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new InvalidOperationException($"Articolo {code} non trovato.");
    }
}
