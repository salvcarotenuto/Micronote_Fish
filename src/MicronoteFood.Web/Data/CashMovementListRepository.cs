using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class CashMovementListRepository(MicronoteDb database)
{
    public async Task<CashMovementListPageModel> GetAsync(
        int year,
        int currentExercise,
        string period,
        string movementType,
        int? paymentMethod,
        int? causeCode,
        int? storeCode,
        int? selectedId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var (dateFrom, dateTo) = PeriodRange(year, period);
        const string sql = """
            SELECT m.ID, m.Anno, m.Settore, m.Codice, m.DataMov,
                   COALESCE(m.Causale, 0) AS Causale,
                   COALESCE(cc.Descrizione, '') AS CausaleDescrizione,
                   COALESCE(m.Annotazioni, '') AS MovimentoDescrizione,
                   COALESCE(m.CliFor, '') AS CliFor,
                   COALESCE(m.Ditta, 0) AS Ditta,
                   CASE COALESCE(m.CliFor, '')
                     WHEN 'C' THEN COALESCE(c.Nome, '')
                     WHEN 'F' THEN COALESCE(f.Nome, '')
                     WHEN 'A' THEN COALESCE(a.Nome, '')
                     ELSE ''
                   END AS DittaNome,
                   COALESCE(m.PuntoV, 0) AS PuntoV,
                   COALESCE(pv.Nome, '') AS PuntoVendita,
                   CASE WHEN m.TipoMov = 'E' THEN COALESCE(m.Importo, 0) ELSE 0 END AS Entrata,
                   CASE WHEN m.TipoMov = 'U' THEN COALESCE(m.Importo, 0) ELSE 0 END AS Uscita,
                   COALESCE(m.ModoPag, 0) AS ModoPag
            FROM MovCassa m
            LEFT JOIN CausaliCassa cc ON cc.Codice = m.Causale
            LEFT JOIN Clienti c ON m.CliFor = 'C' AND c.Codice = m.Ditta
            LEFT JOIN Fornitori f ON m.CliFor = 'F' AND f.Codice = m.Ditta
            LEFT JOIN Agenti a ON m.CliFor = 'A' AND a.Codice = m.Ditta
            LEFT JOIN PuntiVendita pv ON pv.Codice = m.PuntoV
            WHERE m.Anno = @year
              AND (@dateFrom IS NULL OR m.DataMov >= @dateFrom)
              AND (@dateTo IS NULL OR m.DataMov < DATE_ADD(@dateTo, INTERVAL 1 DAY))
              AND (@movementType = '' OR m.TipoMov = @movementType)
              AND (@paymentMethod IS NULL OR m.ModoPag = @paymentMethod)
              AND (@causeCode IS NULL OR m.Causale = @causeCode)
              AND (@storeCode IS NULL OR m.PuntoV = @storeCode)
            ORDER BY m.DataMov DESC, m.Codice DESC, m.ID DESC;
            """;
        var rows = new List<CashMovementListRow>();
        await using (var command = new MySqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("@year", year);
            command.Parameters.Add("@dateFrom", MySqlDbType.Date).Value = dateFrom.HasValue
                ? dateFrom.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            command.Parameters.Add("@dateTo", MySqlDbType.Date).Value = dateTo.HasValue
                ? dateTo.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            command.Parameters.AddWithValue("@movementType", movementType);
            command.Parameters.AddWithValue("@paymentMethod", paymentMethod.HasValue ? paymentMethod.Value : DBNull.Value);
            command.Parameters.AddWithValue("@causeCode", causeCode.HasValue ? causeCode.Value : DBNull.Value);
            command.Parameters.AddWithValue("@storeCode", storeCode.HasValue ? storeCode.Value : DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var payment = Convert.ToInt32(reader["ModoPag"]);
                rows.Add(new(
                    Convert.ToInt32(reader["ID"]),
                    Convert.ToInt32(reader["Anno"]),
                    Convert.ToInt32(reader["Settore"]),
                    Convert.ToInt32(reader["Codice"]),
                    DateOnly.FromDateTime(Convert.ToDateTime(reader["DataMov"])),
                    Convert.ToInt32(reader["Causale"]),
                    Convert.ToString(reader["CausaleDescrizione"]) ?? "",
                    Convert.ToString(reader["MovimentoDescrizione"]) ?? "",
                    Convert.ToString(reader["CliFor"]) ?? "",
                    Convert.ToInt32(reader["Ditta"]),
                    Convert.ToString(reader["DittaNome"]) ?? "",
                    Convert.ToInt32(reader["PuntoV"]),
                    Convert.ToString(reader["PuntoVendita"]) ?? "",
                    Convert.ToDecimal(reader["Entrata"]),
                    Convert.ToDecimal(reader["Uscita"]),
                    payment,
                    payment switch { 1 => "Assegno", 2 => "Bonifico", _ => "Contanti" }));
            }
        }

        return new CashMovementListPageModel
        {
            Year = year,
            Period = period,
            MovementType = movementType,
            PaymentMethod = paymentMethod,
            CauseCode = causeCode,
            StoreCode = storeCode,
            SelectedId = selectedId,
            Years = ListYears(currentExercise),
            Causes = await ListCausesAsync(connection, cancellationToken),
            Stores = await ListStoresAsync(connection, cancellationToken),
            Rows = rows
        };
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand("DELETE FROM MovCassa WHERE ID = @id;", connection);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static (DateOnly? From, DateOnly? To) PeriodRange(int year, string period)
    {
        if (int.TryParse(period, out var month) && month is >= 1 and <= 12)
            return (new DateOnly(year, month, 1), new DateOnly(year, month, DateTime.DaysInMonth(year, month)));

        var today = DateOnly.FromDateTime(DateTime.Today);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return period switch
        {
            "current-week" => (monday, monday.AddDays(6)),
            "previous-week" => (monday.AddDays(-7), monday.AddDays(-1)),
            _ => (null, null)
        };
    }

    private static IReadOnlyList<int> ListYears(int currentYear) =>
        Enumerable.Range(0, 5).Select(offset => currentYear - offset).ToArray();

    private static async Task<IReadOnlyList<CashMovementFilterOption>> ListCausesAsync(MySqlConnection connection, CancellationToken token)
    {
        const string sql = """
            SELECT DISTINCT cc.Codice, COALESCE(cc.Descrizione, '') AS Descrizione
            FROM CausaliCassa cc
            INNER JOIN MovCassa m ON m.Causale = cc.Codice
            ORDER BY Descrizione, Codice;
            """;
        return await ListOptionsAsync(connection, sql, token);
    }

    private static Task<IReadOnlyList<CashMovementFilterOption>> ListStoresAsync(MySqlConnection connection, CancellationToken token) =>
        ListOptionsAsync(connection, "SELECT Codice, COALESCE(Nome, '') AS Descrizione FROM PuntiVendita ORDER BY Codice;", token);

    private static async Task<IReadOnlyList<CashMovementFilterOption>> ListOptionsAsync(MySqlConnection connection, string sql, CancellationToken token)
    {
        var options = new List<CashMovementFilterOption>();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
            options.Add(new(Convert.ToInt32(reader["Codice"]), Convert.ToString(reader["Descrizione"]) ?? ""));
        return options;
    }
}
