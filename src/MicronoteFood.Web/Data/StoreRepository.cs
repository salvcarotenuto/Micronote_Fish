using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class StoreRepository(
    MicronoteDb database,
    ProgressiveCodeService progressiveCodes)
{
    public async Task<IReadOnlyList<StoreListItem>> SearchAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                pv.Codice,
                COALESCE(pv.Nome, '') AS Nome,
                COALESCE(pv.Descrizione, '') AS Descrizione,
                COALESCE(s.Descrizione, '') AS SettoreDescrizione,
                COALESCE(pv.Citta, '') AS Citta,
                COALESCE(pv.Prov, '') AS Prov,
                COALESCE(pv.Contatto, '') AS Contatto,
                COALESCE(pv.Telefono, '') AS Telefono,
                COALESCE(pv.Cellulare, '') AS Cellulare,
                COALESCE(pv.Email, '') AS Email,
                COALESCE(pv.Attivo, 1) AS Attivo
            FROM puntivendita pv
            LEFT JOIN Settori s ON s.Codice = pv.Settore
            WHERE
                @search = ''
                OR pv.Nome LIKE CONCAT('%', @search, '%')
                OR pv.Descrizione LIKE CONCAT('%', @search, '%')
                OR pv.Citta LIKE CONCAT('%', @search, '%')
            ORDER BY pv.Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@search", search?.Trim() ?? "");

        var stores = new List<StoreListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            stores.Add(new StoreListItem(
                Convert.ToInt32(reader["Codice"]),
                reader.GetString("Nome"),
                reader.GetString("Descrizione"),
                reader.GetString("SettoreDescrizione"),
                reader.GetString("Citta"),
                reader.GetString("Prov"),
                reader.GetString("Contatto"),
                reader.GetString("Telefono"),
                reader.GetString("Cellulare"),
                reader.GetString("Email"),
                Convert.ToBoolean(reader["Attivo"])));
        }

        return stores;
    }

    public async Task<StoreEditModel?> GetAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT *
            FROM puntivendita
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

        return new StoreEditModel
        {
            Code = Convert.ToInt32(reader["Codice"]),
            Name = Text(reader, "Nome") ?? "",
            Description = Text(reader, "Descrizione"),
            City = Text(reader, "Citta"),
            PostalCode = Text(reader, "Cap"),
            Address = Text(reader, "Via"),
            Province = Text(reader, "Prov"),
            SectorCode = Integer(reader, "Settore"),
            IsActive = Boolean(reader, "Attivo", true),
            Contact = Text(reader, "Contatto"),
            Phone = Text(reader, "Telefono"),
            Mobile = Text(reader, "Cellulare"),
            Email = Text(reader, "Email")
        };
    }

    public async Task<StoreLookups> GetLookupsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return new StoreLookups(await LoadLookupAsync(
            connection,
            "SELECT Codice, Descrizione FROM Settori ORDER BY Descrizione",
            cancellationToken));
    }

    public async Task<int> InsertAsync(
        StoreEditModel store,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        store.Code = await progressiveCodes.NextCodeAsync(
            connection,
            "puntivendita",
            "Codice",
            cancellationToken: cancellationToken);

        const string sql = """
            INSERT INTO puntivendita
            (
                Codice, Nome, Descrizione, Citta, Cap, Via, Prov,
                Settore, Attivo, Contatto, Telefono, Cellulare, Email
            )
            VALUES
            (
                @code, @name, @description, @city, @postalCode, @address, @province,
                @sectorCode, @isActive, @contact, @phone, @mobile, @email
            );
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, store);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return store.Code;
    }

    public Task<int> NextCodeAsync(CancellationToken cancellationToken = default) =>
        progressiveCodes.NextCodeAsync(
            "puntivendita",
            "Codice",
            cancellationToken: cancellationToken);

    public async Task<bool> UpdateAsync(
        StoreEditModel store,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        const string sql = """
            UPDATE puntivendita
            SET
                Nome = @name,
                Descrizione = @description,
                Citta = @city,
                Cap = @postalCode,
                Via = @address,
                Prov = @province,
                Settore = @sectorCode,
                Attivo = @isActive,
                Contatto = @contact,
                Telefono = @phone,
                Cellulare = @mobile,
                Email = @email
            WHERE Codice = @code;
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, store);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<StoreDeleteResult> DeleteAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        if (await LinkedRowsAsync(connection, "VenditeRg", code, cancellationToken) > 0)
        {
            return new StoreDeleteResult(
                false,
                "Il punto vendita non può essere eliminato: esistono vendite collegate.");
        }

        if (await LinkedRowsAsync(connection, "Carico", code, cancellationToken) > 0)
        {
            return new StoreDeleteResult(
                false,
                "Il punto vendita non può essere eliminato: esistono carichi collegati.");
        }

        await using var command = new MySqlCommand(
            "DELETE FROM puntivendita WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? new StoreDeleteResult(true, "Punto vendita eliminato.")
            : new StoreDeleteResult(false, "Punto vendita non trovato.");
    }

    private static async Task<int> LinkedRowsAsync(
        MySqlConnection connection,
        string tableName,
        int code,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            $"SELECT COUNT(Codice) FROM {tableName} WHERE PuntoV = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static void AddSaveParameters(MySqlCommand command, StoreEditModel store)
    {
        command.Parameters.AddWithValue("@code", store.Code);
        command.Parameters.AddWithValue("@name", Clean(store.Name));
        command.Parameters.AddWithValue("@description", DbText(store.Description));
        command.Parameters.AddWithValue("@city", DbText(store.City));
        command.Parameters.AddWithValue("@postalCode", DbText(store.PostalCode));
        command.Parameters.AddWithValue("@address", DbText(store.Address));
        command.Parameters.AddWithValue("@province", DbText(store.Province));
        command.Parameters.AddWithValue("@sectorCode", DbNumber(store.SectorCode));
        command.Parameters.AddWithValue("@isActive", store.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@contact", DbText(store.Contact));
        command.Parameters.AddWithValue("@phone", DbText(store.Phone));
        command.Parameters.AddWithValue("@mobile", DbText(store.Mobile));
        command.Parameters.AddWithValue("@email", DbText(store.Email));
    }

    private static async Task<IReadOnlyList<LookupOption>> LoadLookupAsync(
        MySqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<LookupOption>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new LookupOption(
                Convert.ToInt32(reader.GetValue(0)),
                reader.IsDBNull(1) ? "" : reader.GetString(1)));
        }

        return items;
    }

    private static string? Text(MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
    }

    private static int? Integer(MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static bool Boolean(
        MySqlDataReader reader,
        string name,
        bool defaultValue)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal)
            ? defaultValue
            : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private static string Clean(string value) => value.Trim();

    private static object DbText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static object DbNumber(int? value) =>
        value.HasValue && value.Value > 0 ? value.Value : DBNull.Value;
}
