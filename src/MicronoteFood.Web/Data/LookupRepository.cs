using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class LookupRepository(MicronoteDb database)
{
    public async Task<LookupRow?> FindAnagraficaAsync(
        string? type,
        int code,
        CancellationToken cancellationToken = default)
    {
        if (!IsSupportedType(type))
        {
            return null;
        }

        var source = LookupSource(type);
        var sql = $"""
            SELECT Codice, Nome, Contropartita, PuntoV, Dettaglio
            FROM ({source}) AS anagrafica
            WHERE Codice = @code
            LIMIT 1;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Row(reader);
    }

    public async Task<IReadOnlyList<LookupRow>> SearchAnagraficheAsync(
        string? type,
        string? search,
        CancellationToken cancellationToken = default)
    {
        if (!IsSupportedType(type))
        {
            return [];
        }

        var source = LookupSource(type);
        var sql = $"""
            SELECT Codice, Nome, Contropartita, PuntoV, Dettaglio
            FROM ({source}) AS anagrafica
            WHERE @search = ''
               OR Nome LIKE CONCAT('%', @search, '%')
               OR CAST(Codice AS CHAR) LIKE CONCAT('%', @search, '%')
               OR Dettaglio LIKE CONCAT('%', @search, '%')
            ORDER BY Nome, Codice
            LIMIT 500;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@search", search?.Trim() ?? "");

        var rows = new List<LookupRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(Row(reader));
        }

        return rows;
    }

    private static bool IsSupportedType(string? type) =>
        string.Equals(type, "fornitori", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "clienti", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "dipendenti", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "banche", StringComparison.OrdinalIgnoreCase);

    private static string LookupSource(string? type) => type?.Trim().ToLowerInvariant() switch
    {
        "clienti" => AnagraficaSource("clienti"),
        "fornitori" => AnagraficaSource("fornitori"),
        "dipendenti" => """
            SELECT Codice,
                   TRIM(CONCAT_WS(' ', NULLIF(COALESCE(Cognome, ''), ''), NULLIF(COALESCE(Nome, ''), ''))) AS Nome,
                   NULL AS Contropartita,
                   PuntoV,
                   TRIM(CONCAT_WS(' ', NULLIF(COALESCE(Citta, ''), ''), NULLIF(COALESCE(Indirizzo, ''), ''))) AS Dettaglio
            FROM dipendenti
            """,
        "banche" => """
            SELECT Codice,
                   COALESCE(Nome, '') AS Nome,
                   NULL AS Contropartita,
                   NULL AS PuntoV,
                   TRIM(CONCAT_WS(' ', NULLIF(COALESCE(Agenzia, ''), ''), NULLIF(COALESCE(Iban, ''), ''))) AS Dettaglio
            FROM banche
            """,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static string AnagraficaSource(string table) => $"""
        SELECT Codice,
               COALESCE(Nome, '') AS Nome,
               Contropartita,
               PuntoV,
               TRIM(CONCAT_WS(' ', NULLIF(COALESCE(Citta, ''), ''), NULLIF(COALESCE(Prov, ''), ''))) AS Dettaglio
        FROM {table}
        """;

    private static LookupRow Row(MySqlDataReader reader)
    {
        var code = reader.GetInt32("Codice");
        return new LookupRow(
            code,
            code.ToString("00000"),
            reader.GetString("Nome"),
            reader.GetString("Dettaglio"),
            reader["Contropartita"] == DBNull.Value ? null : Convert.ToInt32(reader["Contropartita"]),
            reader["PuntoV"] == DBNull.Value ? null : Convert.ToInt32(reader["PuntoV"]));
    }
}
