using System;
using System.Data;
using System.Data.SqlServerCe;

internal static class InspectLegacyStockLoadSchema
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Uso: InspectLegacyStockLoadSchema <MicronoteDb.sdf> <password>");
            return 2;
        }

        var connectionString =
            "Data Source='" + args[0].Replace("'", "''") +
            "';Password='" + args[1].Replace("'", "''") +
            "';Persist Security Info=False;";

        using (var connection = new SqlCeConnection(connectionString))
        {
            connection.Open();
            PrintTable(connection, "Carico");
            PrintTable(connection, "CaricoRg");
        }

        return 0;
    }

    private static void PrintTable(SqlCeConnection connection, string tableName)
    {
        Console.WriteLine("[" + tableName + "]");
        var schema = connection.GetSchema("Columns", new[] { null, null, tableName, null });
        foreach (DataRow row in schema.Rows)
        {
            Console.WriteLine(
                Convert.ToString(row["ORDINAL_POSITION"]) + "\t" +
                Convert.ToString(row["COLUMN_NAME"]) + "\t" +
                Convert.ToString(row["DATA_TYPE"]) + "\t" +
                Convert.ToString(row["CHARACTER_MAXIMUM_LENGTH"]));
        }

        using (var command = new SqlCeCommand("SELECT COUNT(*) FROM [" + tableName + "]", connection))
        {
            Console.WriteLine("RIGHE\t" + Convert.ToString(command.ExecuteScalar()));
        }
    }
}
