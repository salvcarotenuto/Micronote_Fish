using MySqlConnector;

var localConnectionString = Environment.GetEnvironmentVariable("MICRONOTE_LOCAL_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione locale mancante.");
var remoteConnectionString = Environment.GetEnvironmentVariable("MICRONOTE_REMOTE_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione remota mancante.");
var apply = args.Any(arg => arg.Equals("--apply", StringComparison.OrdinalIgnoreCase));

var targets = new List<(string Label, MySqlConnection Connection)>
{
    ("Micronote_0001", new MySqlConnection(
        new MySqlConnectionStringBuilder(localConnectionString) { Database = "Micronote_0001" }.ConnectionString)),
    ("Micronote_master", new MySqlConnection(
        new MySqlConnectionStringBuilder(localConnectionString) { Database = "Micronote_master" }.ConnectionString)),
    ("SmarterASP", new MySqlConnection(remoteConnectionString))
};

try
{
    foreach (var (_, connection) in targets) await connection.OpenAsync();

    var plans = new List<(MySqlConnection Connection, string Label, string[] Columns)>();
    foreach (var (label, connection) in targets)
    {
        var columns = new List<string>();
        foreach (var baseName in new[] { "Imponibile", "Iva", "Totale" })
        {
            var active = await ColumnExistsAsync(connection, baseName);
            var retired = await ColumnExistsAsync(connection, $"_{baseName}");
            if (active == retired)
            {
                throw new InvalidOperationException(
                    $"{label}.Vendite: stato non riconosciuto per {baseName} " +
                    $"(attiva={active}, storica={retired}).");
            }
            columns.Add(active ? baseName : $"_{baseName}");
        }

        var sales = await ScalarAsync(connection, "SELECT COUNT(*) FROM Vendite;");
        var withoutVatRows = await ScalarAsync(
            connection,
            """
            SELECT COUNT(*)
            FROM Vendite v
            LEFT JOIN MovIva m
              ON m.Anno = v.Anno AND m.Codice = v.Codice AND m.Settore = 20
            LEFT JOIN (
                SELECT DISTINCT ID FROM MovIvaRg WHERE Settore = 20
            ) r ON r.ID = m.ID
            WHERE m.ID IS NULL OR r.ID IS NULL;
            """);
        if (withoutVatRows != 0)
        {
            throw new InvalidOperationException(
                $"{label}: {withoutVatRows} vendite su {sales} senza righe IVA.");
        }

        Console.WriteLine(
            $"{label} ({connection.Database}): vendite={sales}, senza righe IVA={withoutVatRows}, " +
            $"colonne da eliminare={string.Join(", ", columns)}");
        plans.Add((connection, label, columns.ToArray()));
    }

    if (!apply)
    {
        Console.WriteLine("Controlli superati. Rieseguire con --apply per eliminare le colonne.");
        return;
    }

    foreach (var (connection, label, columns) in plans)
    {
        var clauses = string.Join(", ", columns.Select(column => $"DROP COLUMN `{column}`"));
        await ExecuteAsync(connection, $"ALTER TABLE Vendite {clauses};");
        foreach (var column in columns)
        {
            if (await ColumnExistsAsync(connection, column))
            {
                throw new InvalidOperationException($"{label}: colonna ancora presente {column}.");
            }
        }
        Console.WriteLine($"{label}: colonne fiscali storiche eliminate da Vendite.");
    }
}
finally
{
    foreach (var (_, connection) in targets) await connection.DisposeAsync();
}

static async Task<bool> ColumnExistsAsync(MySqlConnection connection, string columnName)
{
    await using var command = new MySqlCommand(
        """
        SELECT COUNT(*) FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'vendite' AND COLUMN_NAME = @columnName;
        """,
        connection);
    command.Parameters.AddWithValue("@columnName", columnName);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
}

static async Task<long> ScalarAsync(MySqlConnection connection, string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}

static async Task ExecuteAsync(MySqlConnection connection, string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}
