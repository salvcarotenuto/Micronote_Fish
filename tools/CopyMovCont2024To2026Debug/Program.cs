using MySqlConnector;

var connectionString = Environment.GetEnvironmentVariable("MICRONOTE_DB_CONNECTION")
    ?? throw new InvalidOperationException("Connessione MySQL mancante.");
const int sourceYear = 2024;
const int targetYear = 2026;
const int movementsToCopy = 100;

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

static DateTime? MoveDateToYear(object value, int year)
{
    if (value is null || value == DBNull.Value)
    {
        return null;
    }

    var date = Convert.ToDateTime(value);
    var day = Math.Min(date.Day, DateTime.DaysInMonth(year, date.Month));
    return new DateTime(year, date.Month, day);
}

static async Task<int> ScalarIntAsync(MySqlConnection connection, MySqlTransaction? transaction, string sql)
{
    await using var command = new MySqlCommand(sql, connection, transaction);
    return Convert.ToInt32(await command.ExecuteScalarAsync());
}

static async Task<List<MovementRow>> LoadCandidatesAsync(MySqlConnection connection)
{
    const string sql = """
        SELECT m.ID,
               m.Anno,
               m.Settore,
               m.Codice,
               m.Causale,
               m.DataMov,
               m.CliFor,
               m.Ditta,
               m.NumDoc,
               m.TipoPag,
               m.Titolo,
               m.Documento,
               m.Importo,
               m.PuntoV,
               m.Note,
               COUNT(r.ID) AS Righe,
               SUM(CASE WHEN r.Segno = 'D' THEN 1 ELSE 0 END) AS RigheD,
               SUM(CASE WHEN r.Segno = 'A' THEN 1 ELSE 0 END) AS RigheA
        FROM movcont m
        INNER JOIN movcontrg r ON r.ID = m.ID
        WHERE m.Anno = @sourceYear
        GROUP BY m.ID, m.Anno, m.Settore, m.Codice, m.Causale, m.DataMov,
                 m.CliFor, m.Ditta, m.NumDoc, m.TipoPag, m.Titolo, m.Documento,
                 m.Importo, m.PuntoV, m.Note
        HAVING RigheD > 0 AND RigheA > 0
        ORDER BY m.Causale, m.DataMov, m.Codice;
        """;

    await using var command = new MySqlCommand(sql, connection);
    command.Parameters.AddWithValue("@sourceYear", sourceYear);

    var rows = new List<MovementRow>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        rows.Add(new MovementRow(
            Convert.ToInt32(reader["ID"]),
            Convert.ToInt32(reader["Settore"]),
            Convert.ToInt32(reader["Codice"]),
            Convert.ToInt32(reader["Causale"]),
            MoveDateToYear(reader["DataMov"], targetYear),
            reader["CliFor"] is DBNull ? null : Convert.ToString(reader["CliFor"]),
            reader["Ditta"] is DBNull ? null : Convert.ToInt32(reader["Ditta"]),
            reader["NumDoc"] is DBNull ? null : Convert.ToString(reader["NumDoc"]),
            reader["TipoPag"] is DBNull ? null : Convert.ToString(reader["TipoPag"]),
            reader["Titolo"] is DBNull ? null : Convert.ToString(reader["Titolo"]),
            reader["Documento"] is DBNull ? null : Convert.ToString(reader["Documento"]),
            reader["Importo"] is DBNull ? null : Convert.ToDecimal(reader["Importo"]),
            reader["PuntoV"] is DBNull ? null : Convert.ToInt32(reader["PuntoV"]),
            reader["Note"] is DBNull ? null : Convert.ToString(reader["Note"])));
    }

    return rows;
}

static List<MovementRow> PickDiverseSample(IReadOnlyList<MovementRow> candidates, int count)
{
    var groups = candidates
        .GroupBy(row => row.Cause)
        .OrderBy(group => group.Key)
        .Select(group => new Queue<MovementRow>(group.OrderBy(row => row.MovementDate).ThenBy(row => row.Code)))
        .ToList();
    var selected = new List<MovementRow>();

    while (selected.Count < count && groups.Any(group => group.Count > 0))
    {
        foreach (var group in groups.Where(group => group.Count > 0).ToList())
        {
            selected.Add(group.Dequeue());
            if (selected.Count == count)
            {
                break;
            }
        }
    }

    return selected;
}

static async Task<int> InsertMovementAsync(
    MySqlConnection connection,
    MySqlTransaction transaction,
    MovementRow source,
    int targetCode)
{
    const string sql = """
        INSERT INTO movcont
            (Anno, Settore, Codice, Causale, DataMov, CliFor, Ditta, NumDoc,
             TipoPag, Titolo, Documento, Importo, PuntoV, Note)
        VALUES
            (@year, @sector, @code, @cause, @movementDate, @subjectType, @subjectCode, @documentNumber,
             @paymentType, @title, @document, @amount, @storeCode, @notes);
        SELECT LAST_INSERT_ID();
        """;

    await using var command = new MySqlCommand(sql, connection, transaction);
    command.Parameters.AddWithValue("@year", targetYear);
    command.Parameters.AddWithValue("@sector", source.Sector);
    command.Parameters.AddWithValue("@code", targetCode);
    command.Parameters.AddWithValue("@cause", source.Cause);
    command.Parameters.AddWithValue("@movementDate", source.MovementDate is null ? DBNull.Value : source.MovementDate.Value);
    command.Parameters.AddWithValue("@subjectType", source.SubjectType is null ? DBNull.Value : source.SubjectType);
    command.Parameters.AddWithValue("@subjectCode", source.SubjectCode is null ? DBNull.Value : source.SubjectCode.Value);
    command.Parameters.AddWithValue("@documentNumber", source.DocumentNumber is null ? DBNull.Value : source.DocumentNumber);
    command.Parameters.AddWithValue("@paymentType", source.PaymentType is null ? DBNull.Value : source.PaymentType);
    command.Parameters.AddWithValue("@title", source.Title is null ? DBNull.Value : source.Title);
    command.Parameters.AddWithValue("@document", source.Document is null ? DBNull.Value : source.Document);
    command.Parameters.AddWithValue("@amount", source.Amount is null ? DBNull.Value : source.Amount.Value);
    command.Parameters.AddWithValue("@storeCode", source.StoreCode is null ? DBNull.Value : source.StoreCode.Value);
    command.Parameters.AddWithValue("@notes", source.Notes is null ? DBNull.Value : source.Notes);

    return Convert.ToInt32(await command.ExecuteScalarAsync());
}

static async Task<int> InsertMovementLinesAsync(
    MySqlConnection connection,
    MySqlTransaction transaction,
    int sourceId,
    int targetId,
    int targetYear,
    int targetSector,
    int targetCode)
{
    const string sql = """
        INSERT INTO movcontrg
            (ID, Anno, Settore, Codice, Riga, Conto, Importo, Segno)
        SELECT @targetId,
               @targetYear,
               @targetSector,
               @targetCode,
               Riga,
               Conto,
               Importo,
               Segno
        FROM movcontrg
        WHERE ID = @sourceId
        ORDER BY Segno DESC, Riga ASC;
        """;

    await using var command = new MySqlCommand(sql, connection, transaction);
    command.Parameters.AddWithValue("@targetId", targetId);
    command.Parameters.AddWithValue("@targetYear", targetYear);
    command.Parameters.AddWithValue("@targetSector", targetSector);
    command.Parameters.AddWithValue("@targetCode", targetCode);
    command.Parameters.AddWithValue("@sourceId", sourceId);
    return await command.ExecuteNonQueryAsync();
}

var candidates = await LoadCandidatesAsync(connection);
var selected = PickDiverseSample(candidates, movementsToCopy);

Console.WriteLine($"Candidati {sourceYear} con righe Dare/Avere: {candidates.Count}");
Console.WriteLine($"Selezionati: {selected.Count}");
Console.WriteLine("Causali selezionate:");
foreach (var group in selected.GroupBy(row => row.Cause).OrderBy(group => group.Key))
{
    Console.WriteLine($"  Causale {group.Key}: {group.Count()}");
}

if (selected.Count < movementsToCopy)
{
    Console.WriteLine("ERRORE: candidati insufficienti.");
    return 2;
}

await using var transaction = await connection.BeginTransactionAsync();
try
{
    var nextCode = await ScalarIntAsync(
        connection,
        transaction,
        $"SELECT COALESCE(MAX(Codice), 0) + 1 FROM movcont WHERE Anno = {targetYear};");
    var copiedLines = 0;

    foreach (var source in selected)
    {
        var targetCode = nextCode++;
        var targetId = await InsertMovementAsync(connection, transaction, source, targetCode);
        copiedLines += await InsertMovementLinesAsync(
            connection,
            transaction,
            source.Id,
            targetId,
            targetYear,
            source.Sector,
            targetCode);

        Console.WriteLine(
            $"ID {source.Id} {sourceYear}/{source.Code:000000} -> ID {targetId} {targetYear}/{targetCode:000000} causale {source.Cause}");
    }

    await transaction.CommitAsync();
    Console.WriteLine($"Movimenti copiati: {selected.Count}");
    Console.WriteLine($"Righe dettaglio copiate: {copiedLines}");
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

return 0;

internal sealed record MovementRow(
    int Id,
    int Sector,
    int Code,
    int Cause,
    DateTime? MovementDate,
    string? SubjectType,
    int? SubjectCode,
    string? DocumentNumber,
    string? PaymentType,
    string? Title,
    string? Document,
    decimal? Amount,
    int? StoreCode,
    string? Notes);
