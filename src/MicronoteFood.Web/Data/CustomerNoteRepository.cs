using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class CustomerNoteRepository(MicronoteDb database)
{
    private const string LastProcessingDateKey = "DataUltimaElaborazioneNoteClienti";
    private const string PrintFormatKey = "FormatoStampaNotaCliente";

    public async Task<string> GetPrintFormatAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await EnsurePrintFormatOptionAsync(connection, cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT Valore FROM Opzioni WHERE Chiave = @key LIMIT 1;", connection);
        command.Parameters.AddWithValue("@key", PrintFormatKey);
        var value = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
        return string.Equals(value, "A5", StringComparison.OrdinalIgnoreCase) ? "a5" : "a4";
    }

    public async Task SavePrintFormatAsync(
        string? format,
        CancellationToken cancellationToken = default)
    {
        var value = string.Equals(format, "a5", StringComparison.OrdinalIgnoreCase) ? "A5" : "A4";
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            INSERT INTO Opzioni (Chiave, Valore)
            VALUES (@key, @value)
            ON DUPLICATE KEY UPDATE Valore = @value;
            """, connection);
        command.Parameters.AddWithValue("@key", PrintFormatKey);
        command.Parameters.AddWithValue("@value", value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DateOnly?> GetLastProcessingDateAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await EnsureLastProcessingDateOptionAsync(connection, cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT Valore FROM Opzioni WHERE Chiave = @key LIMIT 1;",
            connection);
        command.Parameters.AddWithValue("@key", LastProcessingDateKey);
        var value = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
        return DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var date)
                ? date
                : null;
    }

    public async Task SaveLastProcessingDateAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await EnsureLastProcessingDateOptionAsync(connection, cancellationToken);
        await using var command = new MySqlCommand(
            "UPDATE Opzioni SET Valore = @value WHERE Chiave = @key;",
            connection);
        command.Parameters.AddWithValue("@key", LastProcessingDateKey);
        command.Parameters.AddWithValue("@value", date.ToString("yyyy-MM-dd"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<CustomerNotePageModel> GetAsync(
        DateOnly dateFrom,
        DateOnly dateTo,
        int? storeCode,
        int? selectedCustomerCode,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var customers = await ListCustomersAsync(
            connection, dateFrom, dateTo, storeCode, cancellationToken);
        var selected = selectedCustomerCode.HasValue
            && customers.Any(row => row.CustomerCode == selectedCustomerCode.Value)
                ? selectedCustomerCode
                : customers.FirstOrDefault()?.CustomerCode;

        return new CustomerNotePageModel
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            StoreCode = storeCode,
            SelectedCustomerCode = selected,
            Stores = await ListStoresAsync(connection, cancellationToken),
            Customers = customers,
            Details = selected.HasValue
                ? await ListDetailsAsync(
                    connection, selected.Value, dateFrom, dateTo, storeCode, cancellationToken)
                : []
        };
    }

    public async Task SaveCashAllowanceAsync(
        int customerCode,
        DateOnly dateFrom,
        DateOnly dateTo,
        decimal discount,
        CancellationToken cancellationToken = default)
    {
        if (customerCode <= 0)
            throw new InvalidOperationException("Cliente non valido.");
        if (discount < 0 || decimal.Round(discount, 2) != discount)
            throw new InvalidOperationException("L'abbuono deve essere positivo e avere al massimo due decimali.");

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);
        const string deleteSql = """
            DELETE FROM MovCassa
            WHERE CliFor = 'C'
              AND Ditta = @customer
              AND Settore = 40
              AND Causale = 5
              AND DataMov BETWEEN @dateFrom AND @dateTo;
            """;
        await using (var delete = new MySqlCommand(deleteSql, connection, transaction))
        {
            AddPeriodParameters(delete, customerCode, dateFrom, dateTo);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        if (discount > 0)
        {
            const string nextCodeSql = """
                SELECT COALESCE(MAX(Codice), 0) + 1
                FROM MovCassa
                WHERE Anno = @year
                FOR UPDATE;
                """;
            await using var nextCode = new MySqlCommand(nextCodeSql, connection, transaction);
            nextCode.Parameters.AddWithValue("@year", dateTo.Year);
            var movementCode = Convert.ToInt32(
                await nextCode.ExecuteScalarAsync(cancellationToken));

            const string insertSql = """
                INSERT INTO MovCassa
                    (Anno, Settore, Codice, DataMov, Causale, TipoMov,
                     CliFor, Ditta, Importo, ModoPag, Documento, PuntoV, Annotazioni)
                VALUES
                    (@year, 40, @code, @movementDate, 5, 'E',
                     'C', @customer, @discount, 0, 0,
                     (SELECT COALESCE(PuntoV, 0) FROM Clienti WHERE Codice = @customer),
                     'Abbuono');
                """;
            await using var insert = new MySqlCommand(insertSql, connection, transaction);
            insert.Parameters.AddWithValue("@year", dateTo.Year);
            insert.Parameters.AddWithValue("@code", movementCode);
            insert.Parameters.AddWithValue("@movementDate", dateTo.ToDateTime(TimeOnly.MinValue));
            insert.Parameters.AddWithValue("@customer", customerCode);
            insert.Parameters.AddWithValue("@discount", discount);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<CustomerNoteSummaryRow>> ListCustomersAsync(
        MySqlConnection connection,
        DateOnly dateFrom,
        DateOnly dateTo,
        int? storeCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH balance_options AS (
                SELECT COALESCE(NULLIF(MAX(CASE WHEN Chiave = 'AnnoSaldoIniCF'
                           THEN CAST(NULLIF(Valore, '') AS UNSIGNED) END), 0), 2000) AS AnnoIniziale
                FROM Opzioni
            ),
            sales_period AS (
                SELECT Cliente,
                       SUM(COALESCE(Merce, 0)) AS Merce,
                       SUM(COALESCE(Iva, 0)) AS Iva,
                       SUM(COALESCE(Totale, 0)) AS Totale
                FROM Vendite
                WHERE DataDoc BETWEEN @dateFrom AND @dateTo
                  AND (@storeCode IS NULL OR PuntoV = @storeCode)
                GROUP BY Cliente
            ),
            cash_period AS (
                SELECT Ditta,
                       SUM(COALESCE(Importo, 0)) AS Incassi,
                       SUM(CASE WHEN Causale = 5 THEN COALESCE(Importo, 0) ELSE 0 END) AS Abbuoni
                FROM MovCassa
                WHERE CliFor = 'C'
                  AND DataMov BETWEEN @dateFrom AND @dateTo
                  AND (@storeCode IS NULL OR PuntoV = @storeCode)
                GROUP BY Ditta
            ),
            previous_sales AS (
                SELECT Cliente, SUM(COALESCE(Totale, 0)) AS Totale
                FROM Vendite, balance_options
                WHERE Anno >= balance_options.AnnoIniziale
                  AND DataDoc < @dateFrom
                GROUP BY Cliente
            ),
            previous_cash AS (
                SELECT Ditta, SUM(COALESCE(Importo, 0)) AS Incassi
                FROM MovCassa, balance_options
                WHERE CliFor = 'C'
                  AND Anno >= balance_options.AnnoIniziale
                  AND DataMov < @dateFrom
                GROUP BY Ditta
            )
            SELECT c.Codice,
                   COALESCE(c.Nome, '') AS Nome,
                   COALESCE(c.Piva, '') AS Piva,
                   COALESCE(sp.Merce, 0) AS Merce,
                   COALESCE(sp.Iva, 0) AS Iva,
                   COALESCE(sp.Totale, 0) AS Totale,
                   COALESCE(c.SaldoIni, 0)
                     + COALESCE(ps.Totale, 0)
                     - COALESCE(pc.Incassi, 0) AS SaldoPrecedente,
                   COALESCE(cp.Incassi, 0) - COALESCE(cp.Abbuoni, 0) AS Pagato,
                   COALESCE(cp.Abbuoni, 0) AS AbbuonoCassa,
                   COALESCE(c.Fido, NULL) AS Fido
            FROM Clienti c
            LEFT JOIN sales_period sp ON sp.Cliente = c.Codice
            LEFT JOIN cash_period cp ON cp.Ditta = c.Codice
            LEFT JOIN previous_sales ps ON ps.Cliente = c.Codice
            LEFT JOIN previous_cash pc ON pc.Ditta = c.Codice
            WHERE sp.Cliente IS NOT NULL OR cp.Ditta IS NOT NULL
            ORDER BY c.Nome, c.Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@dateFrom", dateFrom.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@dateTo", dateTo.ToDateTime(new TimeOnly(23, 59, 59)));
        command.Parameters.AddWithValue("@storeCode", storeCode is null ? DBNull.Value : storeCode.Value);

        var rows = new List<CustomerNoteSummaryRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var total = Money(reader["Totale"]);
            var previous = Money(reader["SaldoPrecedente"]);
            var paid = Money(reader["Pagato"]);
            var cashAllowance = Money(reader["AbbuonoCassa"]);
            rows.Add(new CustomerNoteSummaryRow(
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Nome"]) ?? "",
                Convert.ToString(reader["Piva"]) ?? "",
                Money(reader["Merce"]),
                Money(reader["Iva"]),
                total,
                previous,
                paid,
                cashAllowance,
                decimal.Round(total + previous - paid - cashAllowance, 2),
                reader["Fido"] is DBNull ? null : Money(reader["Fido"])));
        }
        return rows;
    }

    private static async Task<IReadOnlyList<CustomerNoteDetailRow>> ListDetailsAsync(
        MySqlConnection connection,
        int customerCode,
        DateOnly dateFrom,
        DateOnly dateTo,
        int? storeCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT v.ID,
                   v.DataDoc,
                   vr.Articolo,
                   COALESCE(a.Descrizione, '') AS Descrizione,
                   CASE
                       WHEN COALESCE(vr.Quantita, 0) <> 0 THEN vr.Quantita
                       ELSE COALESCE(vr.Colli, 0)
                   END AS Quantita,
                   COALESCE(vr.Prezzo, 0) AS Prezzo,
                   COALESCE(vr.Iva, 0) AS Iva,
                   COALESCE(vr.Importo, 0) AS Importo
            FROM Vendite v
            INNER JOIN VenditeRg vr ON vr.ID = v.ID
            LEFT JOIN Articoli a ON a.Codice = vr.Articolo
            WHERE v.Cliente = @customer
              AND v.DataDoc BETWEEN @dateFrom AND @dateTo
              AND (@storeCode IS NULL OR v.PuntoV = @storeCode)
            ORDER BY v.DataDoc, v.Codice, vr.Riga;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@customer", customerCode);
        command.Parameters.AddWithValue("@dateFrom", dateFrom.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@dateTo", dateTo.ToDateTime(new TimeOnly(23, 59, 59)));
        command.Parameters.AddWithValue("@storeCode", storeCode is null ? DBNull.Value : storeCode.Value);
        var rows = new List<CustomerNoteDetailRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CustomerNoteDetailRow(
                Convert.ToInt32(reader["ID"]),
                DateOnly.FromDateTime(Convert.ToDateTime(reader["DataDoc"])),
                Convert.ToString(reader["Articolo"]) ?? "",
                Convert.ToString(reader["Descrizione"]) ?? "",
                Quantity(reader["Quantita"]),
                Money(reader["Prezzo"]),
                Quantity(reader["Iva"]),
                Money(reader["Importo"])));
        }
        return rows;
    }

    private static async Task<IReadOnlyList<CustomerNoteStoreOption>> ListStoresAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT Codice, COALESCE(Nome, '') AS Nome FROM PuntiVendita ORDER BY Nome, Codice;";
        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<CustomerNoteStoreOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new(Convert.ToInt32(reader["Codice"]), Convert.ToString(reader["Nome"]) ?? ""));
        return rows;
    }

    private static void AddPeriodParameters(
        MySqlCommand command,
        int customerCode,
        DateOnly dateFrom,
        DateOnly dateTo)
    {
        command.Parameters.AddWithValue("@customer", customerCode);
        command.Parameters.AddWithValue("@dateFrom", dateFrom.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@dateTo", dateTo.ToDateTime(new TimeOnly(23, 59, 59)));
    }

    private static decimal Money(object? value) =>
        value is null || value == DBNull.Value ? 0 : decimal.Round(Convert.ToDecimal(value), 2);

    private static decimal Quantity(object? value) =>
        value is null || value == DBNull.Value ? 0 : decimal.Round(Convert.ToDecimal(value), 3);

    private static async Task EnsureLastProcessingDateOptionAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            INSERT INTO Opzioni (Chiave, Valore)
            SELECT @key, ''
            WHERE NOT EXISTS (
                SELECT 1 FROM Opzioni WHERE Chiave = @key
            );
            """,
            connection);
        command.Parameters.AddWithValue("@key", LastProcessingDateKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsurePrintFormatOptionAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            INSERT INTO Opzioni (Chiave, Valore)
            SELECT @key, 'A4'
            WHERE NOT EXISTS (SELECT 1 FROM Opzioni WHERE Chiave = @key);

            UPDATE Opzioni
            SET Valore = 'A4'
            WHERE Chiave = @key AND UPPER(COALESCE(Valore, '')) NOT IN ('A4', 'A5');
            """, connection);
        command.Parameters.AddWithValue("@key", PrintFormatKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
