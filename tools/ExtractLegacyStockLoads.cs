using System;
using System.Data.SqlServerCe;
using System.Globalization;
using System.IO;
using System.Text;

internal static class ExtractLegacyStockLoads
{
    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine(
                "Uso: ExtractLegacyStockLoads <MicronoteDb.sdf> <password> <cartella-output>");
            return 2;
        }

        Directory.CreateDirectory(args[2]);
        var connectionString =
            "Data Source='" + args[0].Replace("'", "''") +
            "';Password='" + args[1].Replace("'", "''") +
            "';Persist Security Info=False;";

        using (var connection = new SqlCeConnection(connectionString))
        {
            connection.Open();
            ExportHeaders(connection, Path.Combine(args[2], "carico.tsv"));
            ExportRows(connection, Path.Combine(args[2], "caricorg.tsv"));
        }

        return 0;
    }

    private static void ExportHeaders(SqlCeConnection connection, string path)
    {
        const string sql =
            "SELECT Anno, Codice, NumDoc, DataDoc, Fornitore, Merce, Iva, Totale " +
            "FROM Carico ORDER BY Anno, Codice";
        using (var command = new SqlCeCommand(sql, connection))
        using (var reader = command.ExecuteReader())
        using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
        {
            while (reader.Read())
            {
                writer.WriteLine(string.Join("\t", new[]
                {
                    Number(reader["Anno"]),
                    Number(reader["Codice"]),
                    Text(reader["NumDoc"]),
                    Date(reader["DataDoc"]),
                    Number(reader["Fornitore"]),
                    Number(reader["Merce"]),
                    Number(reader["Iva"]),
                    Number(reader["Totale"])
                }));
            }
        }
    }

    private static void ExportRows(SqlCeConnection connection, string path)
    {
        const string sql =
            "SELECT Anno, Codice, Riga, Fornitore, DataDoc, Articolo, Ums, " +
            "PesoNt, Prezzo, Sconto, Iva, Importo, Tara, PrNetto, PrIvato " +
            "FROM CaricoRg ORDER BY Anno, Codice, Riga";
        using (var command = new SqlCeCommand(sql, connection))
        using (var reader = command.ExecuteReader())
        using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
        {
            while (reader.Read())
            {
                writer.WriteLine(string.Join("\t", new[]
                {
                    Number(reader["Anno"]),
                    Number(reader["Codice"]),
                    Number(reader["Riga"]),
                    Number(reader["Fornitore"]),
                    Date(reader["DataDoc"]),
                    Text(reader["Articolo"]),
                    Text(reader["Ums"]),
                    Number(reader["PesoNt"]),
                    Number(reader["Prezzo"]),
                    Number(reader["Sconto"]),
                    Number(reader["Iva"]),
                    Number(reader["Importo"]),
                    Number(reader["Tara"]),
                    Number(reader["PrNetto"]),
                    Number(reader["PrIvato"])
                }));
            }
        }
    }

    private static string Text(object value)
    {
        var text = value == null || value == DBNull.Value
            ? ""
            : Convert.ToString(value) ?? "";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(text.TrimEnd()));
    }

    private static string Number(object value)
    {
        return value == null || value == DBNull.Value
            ? "0"
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0";
    }

    private static string Date(object value)
    {
        return value == null || value == DBNull.Value
            ? ""
            : Convert.ToDateTime(value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
