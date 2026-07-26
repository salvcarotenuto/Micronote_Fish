using System;
using System.Data.SqlServerCe;
using System.Globalization;
using System.IO;
using System.Text;

internal static class ExtractSqlCe
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Uso: ExtractSqlCe <origine.sdf> <destinazione.tsv>");
            return 2;
        }

        var source = Path.GetFullPath(args[0]);
        var destination = Path.GetFullPath(args[1]);
        var password = Environment.GetEnvironmentVariable("MICROFISH_LEGACY_DB_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine(
                "Variabile MICROFISH_LEGACY_DB_PASSWORD non configurata.");
            return 3;
        }
        var connectionString = "Data Source='" + source.Replace("'", "''") +
            "';Max Database Size=4048;Persist Security Info=False;Password='" +
            password.Replace("'", "''") + "'";

        using (var connection = new SqlCeConnection(connectionString))
        {
            connection.Open();
            using (var command = new SqlCeCommand(
                "SELECT ID, Anno, Settore, Codice, PuntoV, AliqIva, Imponibile, Iva FROM MovivaRg", connection))
            using (var reader = command.ExecuteReader())
            using (var writer = new StreamWriter(destination, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("ID\tAnno\tSettore\tCodice\tPuntoV\tAliqIva\tImponibile\tIva");
                long rows = 0;
                while (reader.Read())
                {
                    for (var index = 0; index < 8; index++)
                    {
                        if (index > 0) writer.Write('\t');
                        var value = reader.IsDBNull(index) ? "" : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture);
                        writer.Write((value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " "));
                    }
                    writer.WriteLine();
                    rows++;
                }
                Console.WriteLine("Righe estratte: " + rows.ToString(CultureInfo.InvariantCulture));
            }
        }
        return 0;
    }
}
