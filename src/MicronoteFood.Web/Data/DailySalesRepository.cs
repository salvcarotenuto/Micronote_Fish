using System.Globalization;
using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class DailySalesRepository(MicronoteDb database)
{
    private const decimal DefaultVatRate = 10m;

    public async Task<DateOnly?> LastDateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand("SELECT MAX(DataMov) FROM Vendite;", connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : DateOnly.FromDateTime(Convert.ToDateTime(value));
    }

    public async Task<DailySalesPageModel> GetAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var vatRate = await VatRateAsync(connection, cancellationToken);
        var code = 0;
        var year = date.Year;

        await using (var command = new MySqlCommand("SELECT Anno, Codice FROM Vendite WHERE DataMov = @date ORDER BY Codice DESC LIMIT 1;", connection))
        {
            command.Parameters.Add("@date", MySqlDbType.Date).Value = date.ToDateTime(TimeOnly.MinValue);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                year = Convert.ToInt32(reader["Anno"]);
                code = Convert.ToInt32(reader["Codice"]);
            }
        }

        var rows = new List<DailySalesRow>();
        const string sql = """
            SELECT pv.Codice AS PuntoV, COALESCE(pv.Nome, '') AS Nome,
                   COALESCE(mi.Imponibile, 0) AS ImponibileIva, COALESCE(mi.Iva, 0) AS Iva,
                   COALESCE(mi.NonImponibile, 0) AS NonImponibile,
                   COALESCE(vr.Contanti, 0) AS Contanti,
                   COALESCE(vr.Carta, 0) AS Carta, COALESCE(vr.Tickets, 0) AS Tickets,
                   COALESCE(vr.Assegni, 0) AS Assegni, COALESCE(vr.Altro, 0) AS Altro,
                   COALESCE(vr.Sospesi, 0) AS Sospesi, COALESCE(vr.Perdite, 0) AS Perdite
            FROM PuntiVendita pv
            LEFT JOIN VenditeRg vr ON vr.PuntoV = pv.Codice AND vr.Anno = @year AND vr.Codice = @code
            LEFT JOIN (
                SELECT Anno, Codice, PuntoV,
                       SUM(CASE WHEN COALESCE(AliqIva, 0) <> 0 OR COALESCE(Iva, 0) <> 0 THEN Imponibile ELSE 0 END) AS Imponibile,
                       SUM(CASE
                               WHEN COALESCE(Iva, 0) <> 0 THEN Iva
                               WHEN COALESCE(AliqIva, 0) <> 0 THEN ROUND(Imponibile * AliqIva / 100, 2)
                               ELSE 0
                           END) AS Iva,
                       SUM(CASE WHEN COALESCE(AliqIva, 0) = 0 AND COALESCE(Iva, 0) = 0 THEN Imponibile ELSE 0 END) AS NonImponibile
                FROM MovivaRg
                WHERE Settore = 20
                GROUP BY Anno, Codice, PuntoV
            ) mi ON mi.Anno = @year AND mi.Codice = @code AND mi.PuntoV = pv.Codice
            WHERE COALESCE(pv.Attivo, 1) <> 0
            ORDER BY pv.Codice;
            """;
        await using (var command = new MySqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("@year", year);
            command.Parameters.AddWithValue("@code", code);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var taxableNet = Money(reader["ImponibileIva"]);
                var vat = Money(reader["Iva"]);
                var other = Money(reader["Altro"]);
                var exempt = Money(reader["NonImponibile"]);
                var net = taxableNet + exempt;
                var total = net + vat;
                rows.Add(new DailySalesRow(Convert.ToInt32(reader["PuntoV"]), Convert.ToString(reader["Nome"]) ?? "",
                    taxableNet + vat, exempt, net, vat, total, Money(reader["Contanti"]), Money(reader["Carta"]),
                    Money(reader["Tickets"]), Money(reader["Assegni"]), other,
                    Money(reader["Sospesi"]), Money(reader["Perdite"])));
            }
        }

        return new DailySalesPageModel { MovementDate = date, Code = code, VatRate = vatRate, Rows = rows,
            Totals = new(rows.Sum(x => x.TaxableGross), rows.Sum(x => x.Exempt), rows.Sum(x => x.Net), rows.Sum(x => x.Vat),
                rows.Sum(x => x.Total), rows.Sum(x => x.Cash), rows.Sum(x => x.Card), rows.Sum(x => x.Tickets),
                rows.Sum(x => x.Checks), rows.Sum(x => x.Other), rows.Sum(x => x.Suspended), rows.Sum(x => x.Losses)) };
    }

    private static async Task<decimal> VatRateAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "SELECT Valore FROM Opzioni WHERE Chiave IN ('AliqIvaVendite', 'AliquotaIvaVendite') ORDER BY Chiave LIMIT 1;";
        try
        {
            await using var command = new MySqlCommand(sql, connection);
            var value = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var rate) && rate > 0 ? rate : DefaultVatRate;
        }
        catch (MySqlException) { return DefaultVatRate; }
    }

    private static decimal Money(object value) => value is DBNull ? 0 : Convert.ToDecimal(value);
}
