using System;
using System.Data.SqlServerCe;
using System.Globalization;
using System.IO;
using System.Text;

internal static class ExtractLegacyMovCassa
{
    private static int Main(string[] args)
    {
        if (args.Length != 2) return 2;
        var password = Environment.GetEnvironmentVariable("MICROFISH_LEGACY_DB_PASSWORD");
        if (string.IsNullOrWhiteSpace(password)) return 3;
        var cs = "Data Source='" + Path.GetFullPath(args[0]).Replace("'", "''") +
            "';Max Database Size=4048;Persist Security Info=False;Password='" + password.Replace("'", "''") + "'";
        const string sql = "SELECT Anno, Settore, Codice, DataMov, Causale, TipoMov, CliFor, Ditta, Importo, ModoPag, NumDoc, DataDoc, Descrizione FROM MOVCASSA ORDER BY Anno, Settore, Codice";
        using (var connection = new SqlCeConnection(cs))
        using (var command = new SqlCeCommand(sql, connection))
        using (var writer = new StreamWriter(args[1], false, new UTF8Encoding(false)))
        {
            connection.Open();
            using (var reader = command.ExecuteReader())
            {
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    if (i > 0) writer.Write('\t');
                    writer.Write(reader.GetName(i));
                }
                writer.WriteLine();
                var rows = 0;
                while (reader.Read())
                {
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        if (i > 0) writer.Write('\t');
                        var value = reader.GetValue(i);
                        if (reader.IsDBNull(i)) writer.Write("N:");
                        else if (value is string) writer.Write("S:" + Convert.ToBase64String(Encoding.UTF8.GetBytes((string)value)));
                        else if (value is DateTime) writer.Write("D:" + ((DateTime)value).ToString("O", CultureInfo.InvariantCulture));
                        else writer.Write("V:" + Convert.ToString(value, CultureInfo.InvariantCulture));
                    }
                    writer.WriteLine();
                    rows++;
                }
                Console.WriteLine("MOVCASSA estratta: " + rows + " record.");
            }
        }
        return 0;
    }
}
