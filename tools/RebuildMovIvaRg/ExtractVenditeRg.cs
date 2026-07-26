using System;
using System.Data.SqlServerCe;
using System.Globalization;
using System.IO;
using System.Text;

internal static class ExtractVenditeRg
{
    private static int Main(string[] args)
    {
        var password = Environment.GetEnvironmentVariable("MICROFISH_LEGACY_DB_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine(
                "Variabile MICROFISH_LEGACY_DB_PASSWORD non configurata.");
            return 3;
        }
        var cs = "Data Source='" + args[0].Replace("'", "''") +
            "';Max Database Size=4048;Persist Security Info=False;Password='" +
            password.Replace("'", "''") + "'";
        using (var connection = new SqlCeConnection(cs))
        using (var writer = new StreamWriter(args[1], false, new UTF8Encoding(false)))
        {
            connection.Open();
            var sql = "SELECT Anno,Codice,PuntoV,Imponibile,Iva,Totale,Contanti,Carta,Tickets,Assegni,Altro,Sospesi,Perdite FROM VenditeRg";
            using (var command = new SqlCeCommand(sql, connection))
            using (var reader = command.ExecuteReader())
            {
                writer.WriteLine("Anno\tCodice\tPuntoV\tImponibile\tIva\tTotale\tContanti\tCarta\tTickets\tAssegni\tAltro\tSospesi\tPerdite");
                var rows = 0;
                while (reader.Read())
                {
                    for (var index = 0; index < reader.FieldCount; index++)
                    {
                        if (index > 0) writer.Write('\t');
                        writer.Write(reader.IsDBNull(index) ? "" : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture));
                    }
                    writer.WriteLine(); rows++;
                }
                Console.WriteLine("Righe VenditeRg estratte: " + rows);
            }
        }
        return 0;
    }
}
