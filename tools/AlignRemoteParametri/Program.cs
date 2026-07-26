using MySqlConnector;

var localConnectionString = Environment.GetEnvironmentVariable("MICRONOTE_LOCAL_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione locale mancante.");
var remoteConnectionString = Environment.GetEnvironmentVariable("MICRONOTE_REMOTE_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione remota mancante.");
localConnectionString = new MySqlConnectionStringBuilder(localConnectionString)
{
    Database = "Micronote_master"
}.ConnectionString;

await using var local = new MySqlConnection(localConnectionString);
await using var remote = new MySqlConnection(remoteConnectionString);
await local.OpenAsync();
await remote.OpenAsync();

await using (var count = new MySqlCommand("SELECT COUNT(*) FROM Parametri;", remote))
{
    var rows = Convert.ToInt64(await count.ExecuteScalarAsync());
    if (rows != 0)
    {
        throw new InvalidOperationException(
            $"La tabella remota Parametri contiene {rows} righe: nessuna modifica eseguita.");
    }
}

string createSql;
await using (var show = new MySqlCommand("SHOW CREATE TABLE Parametri;", local))
await using (var reader = await show.ExecuteReaderAsync())
{
    await reader.ReadAsync();
    createSql = reader.GetString(1);
}

await using (var drop = new MySqlCommand("DROP TABLE Parametri;", remote))
{
    await drop.ExecuteNonQueryAsync();
}
await using (var create = new MySqlCommand(createSql, remote))
{
    await create.ExecuteNonQueryAsync();
}

await using (var verify = new MySqlCommand("SELECT COUNT(*) FROM Parametri;", remote))
{
    if (Convert.ToInt64(await verify.ExecuteScalarAsync()) != 0)
    {
        throw new InvalidOperationException("Verifica finale non superata.");
    }
}

Console.WriteLine("Parametri remota allineata alla struttura locale e mantenuta vuota.");
