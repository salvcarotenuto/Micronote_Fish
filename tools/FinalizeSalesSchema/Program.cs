using MySqlConnector;

var baseConnection = Environment.GetEnvironmentVariable("MICRONOTE_MIGRATION_CONNECTION")
    ?? throw new InvalidOperationException("Connessione mancante.");
var connectionString = new MySqlConnectionStringBuilder(baseConnection)
{
    Database = "Micronote_0001"
}.ConnectionString;

const string salesBackup = "Vendite_PreNormalize_20260718";
const string rowsBackup = "VenditeRg_PreNormalize_20260718";

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

foreach (var backup in new[] { salesBackup, rowsBackup })
{
    if (await ScalarAsync("""
        SELECT COUNT(*) FROM information_schema.tables
        WHERE table_schema = DATABASE() AND table_name = @name;
        """, ("@name", backup)) > 0)
    {
        throw new InvalidOperationException($"Il backup {backup} esiste già: operazione interrotta.");
    }
}

await ExecuteAsync($"CREATE TABLE {salesBackup} LIKE Vendite;");
await ExecuteAsync($"INSERT INTO {salesBackup} SELECT * FROM Vendite;");
await ExecuteAsync($"CREATE TABLE {rowsBackup} LIKE VenditeRg;");
await ExecuteAsync($"INSERT INTO {rowsBackup} SELECT * FROM VenditeRg;");

var salesRows = await ScalarAsync("SELECT COUNT(*) FROM Vendite;");
var salesBackupRows = await ScalarAsync($"SELECT COUNT(*) FROM {salesBackup};");
var detailRows = await ScalarAsync("SELECT COUNT(*) FROM VenditeRg;");
var detailBackupRows = await ScalarAsync($"SELECT COUNT(*) FROM {rowsBackup};");
if (salesRows != salesBackupRows || detailRows != detailBackupRows)
{
    throw new InvalidOperationException("Conteggi dei backup non coerenti: eliminazione annullata.");
}

await ExecuteAsync("ALTER TABLE Vendite DROP COLUMN Imponibile, DROP COLUMN Iva, DROP COLUMN Totale;");
await ExecuteAsync("ALTER TABLE VenditeRg DROP COLUMN Imponibile, DROP COLUMN Iva, DROP COLUMN Totale;");

var remaining = await ScalarAsync("""
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name IN ('Vendite', 'VenditeRg')
      AND column_name IN ('Imponibile', 'Iva', 'Totale');
    """);

Console.WriteLine($"Vendite: {salesRows} righe; backup: {salesBackupRows}.");
Console.WriteLine($"VenditeRg: {detailRows} righe; backup: {detailBackupRows}.");
Console.WriteLine($"Colonne fiscali residue: {remaining}.");
Console.WriteLine($"Backup creati: {salesBackup}, {rowsBackup}.");

async Task ExecuteAsync(string sql)
{
    await using var command = new MySqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}

async Task<long> ScalarAsync(string sql, params (string Name, object Value)[] parameters)
{
    await using var command = new MySqlCommand(sql, connection);
    foreach (var parameter in parameters)
    {
        command.Parameters.AddWithValue(parameter.Name, parameter.Value);
    }

    return Convert.ToInt64(await command.ExecuteScalarAsync());
}
