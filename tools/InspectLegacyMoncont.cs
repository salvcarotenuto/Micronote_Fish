using System;
using System.Data;
using System.Data.SqlServerCe;
using System.Globalization;
using System.IO;

internal static class InspectLegacyMoncont
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Uso: InspectLegacyMoncont <MicronoteDb.sdf> <tabella>");
            return 2;
        }

        var password = Environment.GetEnvironmentVariable("MICROFISH_LEGACY_DB_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Password legacy non configurata.");
            return 3;
        }

        var cs = "Data Source='" + Path.GetFullPath(args[0]).Replace("'", "''") +
            "';Max Database Size=4048;Persist Security Info=False;Password='" +
            password.Replace("'", "''") + "'";
        using (var connection = new SqlCeConnection(cs))
        {
            connection.Open();
            var tables = connection.GetSchema("Tables");
            string tableName = null;
            foreach (DataRow table in tables.Rows)
            {
                var candidate = Convert.ToString(table["TABLE_NAME"], CultureInfo.InvariantCulture);
                if (string.Equals(candidate, args[1], StringComparison.OrdinalIgnoreCase))
                {
                    tableName = candidate;
                    break;
                }
            }
            if (tableName == null)
            {
                Console.WriteLine("Tabella " + args[1] + " non trovata. Tabelle affini:");
                foreach (DataRow table in tables.Rows)
                {
                    var candidate = Convert.ToString(table["TABLE_NAME"], CultureInfo.InvariantCulture) ?? "";
                    if (candidate.IndexOf("mon", StringComparison.OrdinalIgnoreCase) >= 0
                        || candidate.IndexOf("mov", StringComparison.OrdinalIgnoreCase) >= 0
                        || candidate.IndexOf("cont", StringComparison.OrdinalIgnoreCase) >= 0
                        || candidate.IndexOf("cassa", StringComparison.OrdinalIgnoreCase) >= 0)
                        Console.WriteLine(candidate);
                }
                return 4;
            }

            var schema = connection.GetSchema("Columns", new[] { null, null, tableName, null });
            if (schema.Rows.Count == 0)
            {
                Console.WriteLine("Tabella " + args[1] + " non trovata.");
                return 4;
            }

            Console.WriteLine("COLONNE");
            var rows = schema.Select("", "ORDINAL_POSITION ASC");
            foreach (var row in rows)
            {
                Console.WriteLine(
                    Convert.ToString(row["ORDINAL_POSITION"], CultureInfo.InvariantCulture) + "\t" +
                    Convert.ToString(row["COLUMN_NAME"], CultureInfo.InvariantCulture) + "\t" +
                    Convert.ToString(row["DATA_TYPE"], CultureInfo.InvariantCulture) + "\t" +
                    Convert.ToString(row["CHARACTER_MAXIMUM_LENGTH"], CultureInfo.InvariantCulture) + "\t" +
                    Convert.ToString(row["IS_NULLABLE"], CultureInfo.InvariantCulture));
            }

            using (var count = new SqlCeCommand("SELECT COUNT(*) FROM [" + tableName + "]", connection))
                Console.WriteLine("RIGHE\t" + Convert.ToString(count.ExecuteScalar(), CultureInfo.InvariantCulture));

            using (var years = new SqlCeCommand(
                "SELECT Anno, COUNT(*) AS Righe FROM [" + tableName + "] GROUP BY Anno ORDER BY Anno", connection))
            using (var reader = years.ExecuteReader())
            {
                Console.WriteLine("RIGHE_PER_ANNO");
                while (reader.Read())
                    Console.WriteLine(Convert.ToString(reader[0], CultureInfo.InvariantCulture) + "\t" +
                                      Convert.ToString(reader[1], CultureInfo.InvariantCulture));
            }

            using (var sample = new SqlCeCommand("SELECT TOP (3) * FROM [" + tableName + "] ORDER BY Anno DESC", connection))
            using (var reader = sample.ExecuteReader())
            {
                Console.WriteLine("ESEMPI_CAMPI_NON_NULLI");
                while (reader.Read())
                {
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        if (reader.IsDBNull(i)) continue;
                        var value = reader.GetValue(i);
                        Console.Write(reader.GetName(i) + "=" + Convert.ToString(value, CultureInfo.InvariantCulture) + "; ");
                    }
                    Console.WriteLine();
                }
            }

            PrintScalar(connection, tableName, "DUPLICATI_CHIAVE", "SELECT COUNT(*) FROM (SELECT Anno, Settore, Codice FROM [" + tableName + "] GROUP BY Anno, Settore, Codice HAVING COUNT(*) > 1) D");
            PrintScalar(connection, tableName, "DATE_NULLE", "SELECT COUNT(*) FROM [" + tableName + "] WHERE DataMov IS NULL");
            PrintScalar(connection, tableName, "IMPORTI_NULLI", "SELECT COUNT(*) FROM [" + tableName + "] WHERE Importo IS NULL");
            PrintScalar(connection, tableName, "NUMDOC_VALORIZZATI", "SELECT COUNT(*) FROM [" + tableName + "] WHERE NumDoc IS NOT NULL AND LTRIM(RTRIM(NumDoc)) <> ''");
            PrintScalar(connection, tableName, "DATADOC_VALORIZZATE", "SELECT COUNT(*) FROM [" + tableName + "] WHERE DataDoc IS NOT NULL");
            PrintScalar(connection, tableName, "DESCRIZIONI_VALORIZZATE", "SELECT COUNT(*) FROM [" + tableName + "] WHERE Descrizione IS NOT NULL AND LTRIM(RTRIM(Descrizione)) <> ''");
            PrintGroups(connection, tableName, "TIPOMOV", "TipoMov");
            PrintGroups(connection, tableName, "CLIFOR", "CliFor");
            PrintGroups(connection, tableName, "MODOPAG", "ModoPag");
        }
        return 0;
    }

    private static void PrintScalar(SqlCeConnection connection, string table, string label, string sql)
    {
        using (var command = new SqlCeCommand(sql, connection))
            Console.WriteLine(label + "\t" + Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture));
    }

    private static void PrintGroups(SqlCeConnection connection, string table, string label, string column)
    {
        Console.WriteLine(label);
        using (var command = new SqlCeCommand("SELECT " + column + ", COUNT(*) FROM [" + table + "] GROUP BY " + column + " ORDER BY " + column, connection))
        using (var reader = command.ExecuteReader())
            while (reader.Read())
                Console.WriteLine((reader.IsDBNull(0) ? "NULL" : Convert.ToString(reader[0], CultureInfo.InvariantCulture)) + "\t" + Convert.ToString(reader[1], CultureInfo.InvariantCulture));
    }
}
