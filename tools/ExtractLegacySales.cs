using System;
using System.Data.SqlServerCe;
using System.Globalization;
using System.IO;
using System.Text;

internal static class ExtractLegacySales
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Uso: ExtractLegacySales <MicronoteDb.sdf> <cartella-output>");
            return 2;
        }

        var password = Environment.GetEnvironmentVariable("MICROFISH_LEGACY_DB_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Variabile MICROFISH_LEGACY_DB_PASSWORD non configurata.");
            return 3;
        }

        Directory.CreateDirectory(args[1]);
        var cs = "Data Source='" + args[0].Replace("'", "''") +
            "';Max Database Size=4048;Persist Security Info=False;Password='" +
            password.Replace("'", "''") + "'";
        using (var connection = new SqlCeConnection(cs))
        {
            connection.Open();
            Export(connection, Path.Combine(args[1], "vendite.tsv"),
                "SELECT Anno,Settore,Codice,Stato,NumDoc,DataDoc,Cliente,Merce,Agente," +
                "Liquidato,Provvigione,Iva,Totale,Pagato,Abbuono,PuntoV " +
                "FROM Vendite ORDER BY Anno,Settore,Codice");
            Export(connection, Path.Combine(args[1], "venditerg.tsv"),
                "SELECT Anno,Settore,Codice,Riga,Cliente,DataDoc,Articolo,Fornitore,Mag,Ums," +
                "Colli,PesoLr,Tara,PesoNt,Prezzo,Sconto,Iva,PrNetto,PrIvato,Importo " +
                "FROM VenditeRg ORDER BY Anno,Settore,Codice,Riga");
        }
        return 0;
    }

    private static void Export(SqlCeConnection connection, string path, string sql)
    {
        using (var command = new SqlCeCommand(sql, connection))
        using (var reader = command.ExecuteReader())
        using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
        {
            var rows = 0;
            while (reader.Read())
            {
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    if (i > 0) writer.Write('\t');
                    var value = reader.GetValue(i);
                    if (value == null || value == DBNull.Value) writer.Write("0");
                    else if (value is DateTime) writer.Write(((DateTime)value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    else if (value is string) writer.Write(Convert.ToBase64String(Encoding.UTF8.GetBytes(((string)value).TrimEnd())));
                    else writer.Write(Convert.ToString(value, CultureInfo.InvariantCulture));
                }
                writer.WriteLine();
                rows++;
            }
            Console.WriteLine(Path.GetFileName(path) + ": " + rows + " righe.");
        }
    }
}
