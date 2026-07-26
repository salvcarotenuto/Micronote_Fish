using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class SalesHistoryRepository(MicronoteDb database)
{
    public async Task<SalesHistoryPageModel> GetAsync(
        int year,
        int month,
        DateOnly dateFrom,
        DateOnly dateTo,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var years = await ListYearsAsync(connection, year, cancellationToken);

        var sales = await ListSalesAsync(
            connection,
            dateFrom,
            dateTo,
            cancellationToken);

        return new SalesHistoryPageModel
        {
            Year = year,
            Month = Math.Clamp(month, 0, 12),
            DateFrom = dateFrom,
            DateTo = dateTo,
            Years = years,
            Sales = sales,
            Totals = TotalsFrom(sales)
        };
    }

    private static async Task<IReadOnlyList<int>> ListYearsAsync(
        MySqlConnection connection,
        int currentYear,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT Anno
            FROM Vendite
            WHERE Anno IS NOT NULL
            ORDER BY Anno DESC;
            """;

        var years = new List<int>();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            years.Add(Convert.ToInt32(reader["Anno"]));
        }

        if (!years.Contains(currentYear))
        {
            years.Insert(0, currentYear);
        }

        return years;
    }

    private static async Task<IReadOnlyList<SalesHistoryListItem>> ListSalesAsync(
        MySqlConnection connection,
        DateOnly dateFrom,
        DateOnly dateTo,
        CancellationToken cancellationToken)
    {
        var rows = new List<SalesHistoryListItem>();
        var vatTotals = await ListVatTotalsAsync(connection, cancellationToken);
        const string sql = """
            SELECT v.ID, v.Anno, v.Codice, v.DataMov,
                   COALESCE(vr.Contanti, 0) AS Contanti, COALESCE(vr.Carta, 0) AS Carta,
                   COALESCE(vr.Tickets, 0) AS Tickets, COALESCE(vr.Assegni, 0) AS Assegni,
                   COALESCE(vr.Altro, 0) AS Altro, COALESCE(vr.Sospesi, 0) AS Sospesi,
                   COALESCE(vr.Perdite, 0) AS Perdite,
                   mc.ID AS AccountingMovementId
            FROM Vendite v
            LEFT JOIN (
                SELECT ID, SUM(Contanti) AS Contanti, SUM(Carta) AS Carta,
                       SUM(Tickets) AS Tickets, SUM(Assegni) AS Assegni,
                       SUM(Altro) AS Altro, SUM(Sospesi) AS Sospesi, SUM(Perdite) AS Perdite
                FROM VenditeRg
                GROUP BY ID
            ) vr ON vr.ID = v.ID
            LEFT JOIN MovCont mc ON mc.Settore = 20 AND mc.Documento = v.ID
            WHERE v.DataMov BETWEEN @dateFrom AND @dateTo
            ORDER BY v.DataMov DESC, v.Codice DESC;
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddDateParameters(command, dateFrom, dateTo);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var rowYear = Convert.ToInt32(reader["Anno"]);
            var rowCode = Convert.ToInt32(reader["Codice"]);
            var fiscal = vatTotals.GetValueOrDefault((rowYear, rowCode), VatTotals.Empty);
            rows.Add(new SalesHistoryListItem(
                Convert.ToInt32(reader["ID"]),
                reader["AccountingMovementId"] is DBNull ? null : Convert.ToInt32(reader["AccountingMovementId"]),
                rowYear,
                rowCode,
                DateOnly.FromDateTime(Convert.ToDateTime(reader["DataMov"])),
                fiscal.Net,
                fiscal.NonTaxable,
                fiscal.Vat,
                fiscal.Total,
                Money(reader["Contanti"]),
                Money(reader["Carta"]),
                Money(reader["Tickets"]),
                Money(reader["Assegni"]),
                Money(reader["Altro"]),
                Money(reader["Sospesi"]),
                Money(reader["Perdite"])));
        }

        return rows;
    }

    public async Task<IReadOnlyList<SalesHistoryDetailItem>> ListDetailsAsync(
        int year,
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return await ListDetailsAsync(connection, year, code, cancellationToken);
    }

    private static async Task<IReadOnlyList<SalesHistoryDetailItem>> ListDetailsAsync(
        MySqlConnection connection,
        int year,
        int code,
        CancellationToken cancellationToken)
    {
        var vatTotals = await ListVatDetailsAsync(connection, year, code, cancellationToken);
        const string sql = """
            SELECT vr.PuntoV, COALESCE(pv.Nome, '') AS Nome,
                   vr.Contanti, vr.Carta, vr.Tickets, vr.Assegni, vr.Altro, vr.Sospesi, vr.Perdite
            FROM VenditeRg vr
            LEFT JOIN PuntiVendita pv ON pv.Codice = vr.PuntoV
            WHERE vr.Anno = @year
              AND vr.Codice = @code
            ORDER BY vr.PuntoV;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@code", code);

        var rows = new List<SalesHistoryDetailItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var storeCode = Convert.ToInt32(reader["PuntoV"]);
            var fiscal = vatTotals.GetValueOrDefault(storeCode, VatTotals.Empty);
            rows.Add(new SalesHistoryDetailItem(
                storeCode,
                Convert.ToString(reader["Nome"]) ?? "",
                fiscal.Net,
                fiscal.NonTaxable,
                fiscal.Vat,
                fiscal.Total,
                Money(reader["Contanti"]),
                Money(reader["Carta"]),
                Money(reader["Tickets"]),
                Money(reader["Assegni"]),
                Money(reader["Altro"]),
                Money(reader["Sospesi"]),
                Money(reader["Perdite"])));
        }

        return rows;
    }

    private static async Task<Dictionary<(int Year, int Code), VatTotals>> ListVatTotalsAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT mv.Anno, mv.Codice,
                   SUM(CASE WHEN COALESCE(r.AliqIva, 0) <> 0 THEN COALESCE(r.Imponibile, 0) ELSE 0 END) AS Imponibile,
                   SUM(CASE WHEN COALESCE(r.AliqIva, 0) = 0 THEN COALESCE(r.Imponibile, 0) ELSE 0 END) AS NonImponibile,
                   SUM(COALESCE(r.Iva, 0)) AS Iva
            FROM MovIva mv
            INNER JOIN MovIvaRg r ON r.ID = mv.ID AND r.Settore = 20
            WHERE mv.Settore = 20
            GROUP BY mv.Anno, mv.Codice;
            """;

        var totals = new Dictionary<(int Year, int Code), VatTotals>();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            totals[(Convert.ToInt32(reader["Anno"]), Convert.ToInt32(reader["Codice"]))] =
                VatTotals.From(
                    Money(reader["Imponibile"]),
                    Money(reader["NonImponibile"]),
                    Money(reader["Iva"]));
        }

        return totals;
    }

    private static async Task<Dictionary<int, VatTotals>> ListVatDetailsAsync(
        MySqlConnection connection,
        int year,
        int code,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT r.PuntoV,
                   SUM(CASE WHEN COALESCE(r.AliqIva, 0) <> 0 THEN COALESCE(r.Imponibile, 0) ELSE 0 END) AS Imponibile,
                   SUM(CASE WHEN COALESCE(r.AliqIva, 0) = 0 THEN COALESCE(r.Imponibile, 0) ELSE 0 END) AS NonImponibile,
                   SUM(COALESCE(r.Iva, 0)) AS Iva
            FROM MovIva mv
            INNER JOIN MovIvaRg r ON r.ID = mv.ID AND r.Settore = 20
            WHERE mv.Settore = 20
              AND mv.Anno = @year
              AND mv.Codice = @code
            GROUP BY r.PuntoV;
            """;

        var totals = new Dictionary<int, VatTotals>();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@code", code);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            totals[Convert.ToInt32(reader["PuntoV"])] =
                VatTotals.From(
                    Money(reader["Imponibile"]),
                    Money(reader["NonImponibile"]),
                    Money(reader["Iva"]));
        }

        return totals;
    }

    private sealed record VatTotals(decimal Net, decimal NonTaxable, decimal Vat, decimal Total)
    {
        public static VatTotals Empty { get; } = new(0, 0, 0, 0);

        public static VatTotals From(decimal net, decimal nonTaxable, decimal vat) =>
            new(net, nonTaxable, vat, net + nonTaxable + vat);
    }

    private static SalesHistoryTotals TotalsFrom(IReadOnlyList<SalesHistoryListItem> rows) =>
        new(
            rows.Sum(row => row.Net),
            rows.Sum(row => row.NonTaxable),
            rows.Sum(row => row.Vat),
            rows.Sum(row => row.Total),
            rows.Sum(row => row.Cash),
            rows.Sum(row => row.Card),
            rows.Sum(row => row.Tickets),
            rows.Sum(row => row.Checks),
            rows.Sum(row => row.Other),
            rows.Sum(row => row.Suspended),
            rows.Sum(row => row.Losses));

    private static void AddDateParameters(
        MySqlCommand command,
        DateOnly dateFrom,
        DateOnly dateTo)
    {
        command.Parameters.AddWithValue("@dateFrom", dateFrom.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@dateTo", dateTo.ToDateTime(TimeOnly.MinValue));
    }

    private static decimal Money(object value) =>
        value is null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
}
