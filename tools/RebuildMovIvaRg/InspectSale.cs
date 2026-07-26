using System;
using System.Data.SqlServerCe;
using System.Globalization;

internal static class InspectSale
{
    private static int Main(string[] args)
    {
        var source = args[0];
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
            Dump(connection, "VenditeRg", "SELECT * FROM VenditeRg WHERE Anno=2025 AND Codice=361 ORDER BY PuntoV");
            Dump(connection, "MovivaRg", "SELECT * FROM MovivaRg WHERE Anno=2025 AND Settore=20 AND Codice=361 ORDER BY PuntoV, AliqIva");
        }
        return 0;
    }

    private static void Dump(SqlCeConnection connection, string title, string sql)
    {
        Console.WriteLine("[" + title + "]");
        using (var command = new SqlCeCommand(sql, connection))
        using (var reader = command.ExecuteReader())
        {
            for (var index = 0; index < reader.FieldCount; index++)
            {
                if (index > 0) Console.Write('\t');
                Console.Write(reader.GetName(index));
            }
            Console.WriteLine();
            while (reader.Read())
            {
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    if (index > 0) Console.Write('\t');
                    Console.Write(reader.IsDBNull(index) ? "" : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture));
                }
                Console.WriteLine();
            }
        }
    }
}
