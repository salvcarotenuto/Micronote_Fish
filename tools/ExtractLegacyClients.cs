using System;
using System.Data.SqlServerCe;
using System.Globalization;
using System.IO;
using System.Text;

internal static class ExtractLegacyClients
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Uso: ExtractLegacyClients <origine.sdf> <destinazione.tsv>");
            return 2;
        }

        var password = Environment.GetEnvironmentVariable("MICROFISH_LEGACY_DB_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Variabile MICROFISH_LEGACY_DB_PASSWORD non configurata.");
            return 3;
        }

        var connectionString = "Data Source='" + Path.GetFullPath(args[0]).Replace("'", "''") +
            "';Max Database Size=4048;Persist Security Info=False;Password='" +
            password.Replace("'", "''") + "'";
        const string sql =
            "SELECT Codice, Nome, CodFi, Piva, Citta, Cap, Provincia, Via, " +
            "Telefono1, Telefono2, Email, Pec, PuntoV, Attivo, Categoria, " +
            "Listino, Fido, SaldoIni, CodSdi, Nazione, Natura, Contropartita, " +
            "Agente, Pagamento, Banca FROM Clienti ORDER BY Codice";
        const string headers =
            "Codice\tNome\tCodFi\tPiva\tCitta\tCap\tProvincia\tVia\t" +
            "Telefono1\tTelefono2\tEmail\tPec\tPuntoV\tAttivo\tCategoria\t" +
            "Listino\tFido\tSaldoIni\tCodSdi\tNazione\tNatura\tContropartita\t" +
            "Agente\tPagamento\tBanca";

        using (var connection = new SqlCeConnection(connectionString))
        using (var command = new SqlCeCommand(sql, connection))
        using (var writer = new StreamWriter(args[1], false, new UTF8Encoding(false)))
        {
            connection.Open();
            using (var reader = command.ExecuteReader())
            {
                writer.WriteLine(headers);
                while (reader.Read())
                {
                    for (var index = 0; index < reader.FieldCount; index++)
                    {
                        if (index > 0) writer.Write('\t');
                        var value = reader.IsDBNull(index)
                            ? ""
                            : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture);
                        writer.Write((value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " "));
                    }
                    writer.WriteLine();
                }
            }
        }

        return 0;
    }
}
