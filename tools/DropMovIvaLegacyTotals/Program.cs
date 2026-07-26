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
    foreach (var (_, connection) in targets)
    {
        await connection.OpenAsync();
    }

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
                    $"{label}.MovIva: stato non riconosciuto per {baseName} " +
                    $"(attiva={active}, storica={retired}).");
            }
            columns.Add(active ? baseName : $"_{baseName}");
        }

        var headers = await ScalarAsync(
            connection,
            "SELECT COUNT(*) FROM MovIva WHERE Settore IN (10, 20);");
        var withoutRows = await ScalarAsync(
            connection,
            """
            SELECT COUNT(*)
            FROM MovIva m
            LEFT JOIN (
                SELECT DISTINCT ID FROM MovIvaRg WHERE Settore IN (10, 20)
            ) r ON r.ID = m.ID
            WHERE m.Settore IN (10, 20) AND r.ID IS NULL;
            """);
        if (withoutRows != 0)
        {
            throw new InvalidOperationException(
                $"{label}: {withoutRows} testate operative su {headers} senza righe MovIvaRg.");
        }

        Console.WriteLine(
            $"{label} ({connection.Database}): testate={headers}, senza righe IVA={withoutRows}, " +
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
        await ExecuteAsync(connection, $"ALTER TABLE MovIva {clauses};");
        foreach (var column in columns)
        {
            if (await ColumnExistsAsync(connection, column))
            {
                throw new InvalidOperationException($"{label}: colonna ancora presente {column}.");
            }
        }
        Console.WriteLine($"{label}: colonne fiscali storiche eliminate.");
    }
}
finally
{
    foreach (var (_, connection) in targets)
    {
        await connection.DisposeAsync();
    }
}

static async Task<bool> ColumnExistsAsync(MySqlConnection connection, string columnName)
{
    await using var command = new MySqlCommand(
        """
        SELECT COUNT(*) FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'moviva' AND COLUMN_NAME = @columnName;
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
