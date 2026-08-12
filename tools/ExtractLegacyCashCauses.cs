using System;
using System.Data;
using System.Data.SqlServerCe;
using System.Globalization;
using System.IO;
using System.Text;

internal static class ExtractLegacyCashCauses
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Uso: ExtractLegacyCashCauses <MicronoteDb.sdf> <destinazione.tsv>");
            return 2;
        }

        var password = Environment.GetEnvironmentVariable("MICROFISH_LEGACY_DB_PASSWORD");
        var cs = "Data Source='" + Path.GetFullPath(args[0]).Replace("'", "''") +
            "';Max Database Size=4048;Persist Security Info=False" +
            (string.IsNullOrWhiteSpace(password)
                ? ""
                : ";Password='" + password.Replace("'", "''") + "'");
        using (var connection = new SqlCeConnection(cs))
        {
            connection.Open();
            var schema = connection.GetSchema("Columns", new[] { null, null, "CausaliCont", null });
            foreach (DataRow row in schema.Rows)
            {
                Console.WriteLine(
                    Convert.ToString(row["ORDINAL_POSITION"]) + "\t" +
                    Convert.ToString(row["COLUMN_NAME"]) + "\t" +
                    Convert.ToString(row["DATA_TYPE"]) + "\t" +
                    Convert.ToString(row["CHARACTER_MAXIMUM_LENGTH"]));
            }

            using (var command = new SqlCeCommand("SELECT * FROM CausaliCont ORDER BY Codice", connection))
            using (var reader = command.ExecuteReader())
            using (var writer = new StreamWriter(args[1], false, new UTF8Encoding(false)))
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
                        if (value == null || value == DBNull.Value) writer.Write("N:");
                        else if (value is string) writer.Write("S:" + Convert.ToBase64String(Encoding.UTF8.GetBytes((string)value)));
                        else writer.Write("V:" + Convert.ToString(value, CultureInfo.InvariantCulture));
                    }
                    writer.WriteLine();
                    rows++;
                }
                Console.WriteLine("Righe estratte: " + rows);
            }
        }
        return 0;
    }
}
