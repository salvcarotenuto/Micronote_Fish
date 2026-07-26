using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class BankRepository(MicronoteDb database, ProgressiveCodeService progressiveCodes)
{
    public async Task<IReadOnlyList<BankListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Codice, COALESCE(Nome, '') Nome, COALESCE(Agenzia, '') Agenzia,
                   COALESCE(Abi, '') Abi, COALESCE(Cab, '') Cab, COALESCE(Conto, '') Conto
            FROM Banche ORDER BY Nome;
            """;
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<BankListItem>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new(Convert.ToInt32(reader["Codice"]), Text(reader, "Nome"), Text(reader, "Agenzia"), Text(reader, "Abi"), Text(reader, "Cab"), Text(reader, "Conto")));
        return rows;
    }

    public async Task<BankEditModel?> GetAsync(int code, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand("SELECT * FROM Banche WHERE Codice=@code LIMIT 1;", connection);
        command.Parameters.AddWithValue("@code", code);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new BankEditModel
        {
            Code = code, Name = Text(reader, "Nome"), Branch = Text(reader, "Agenzia"), Website = Text(reader, "SitoWeb"),
            Email = Text(reader, "Email"), Phone = Text(reader, "Telefono"), Mobile = Text(reader, "Cellulare"),
            Abi = Text(reader, "Abi"), Cab = Text(reader, "Cab"), Swift = Text(reader, "Swift"), Sia = Text(reader, "Sia"),
            Account = Text(reader, "Conto"), Iban = Text(reader, "Iban"), Notes = Text(reader, "Notes")
        };
    }

    public Task<int> NextCodeAsync(CancellationToken cancellationToken = default) =>
        progressiveCodes.NextCodeAsync("Banche", "Codice", cancellationToken: cancellationToken);

    public async Task<int> InsertAsync(BankEditModel bank, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        bank.Code = await progressiveCodes.NextCodeAsync(connection, "Banche", "Codice", cancellationToken: cancellationToken);
        const string sql = """
            INSERT INTO Banche (Codice,Nome,Agenzia,SitoWeb,Email,Telefono,Cellulare,Abi,Cab,Swift,Sia,Conto,Iban,Notes)
            VALUES (@code,@name,@branch,@website,@email,@phone,@mobile,@abi,@cab,@swift,@sia,@account,@iban,@notes);
            """;
        await using var command = new MySqlCommand(sql, connection); AddParameters(command, bank);
        await command.ExecuteNonQueryAsync(cancellationToken); return bank.Code;
    }

    public async Task<bool> UpdateAsync(BankEditModel bank, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        const string sql = """
            UPDATE Banche SET Nome=@name,Agenzia=@branch,SitoWeb=@website,Email=@email,Telefono=@phone,
              Cellulare=@mobile,Abi=@abi,Cab=@cab,Swift=@swift,Sia=@sia,Conto=@account,Iban=@iban,Notes=@notes
            WHERE Codice=@code;
            """;
        await using var command = new MySqlCommand(sql, connection); AddParameters(command, bank);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<BankDeleteResult> DeleteAsync(int code, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        foreach (var linked in new[] { (Table: "Fatture", Label: "fatture di vendita"), (Table: "MovIva", Label: "fatture di acquisto") })
        {
            await using var count = new MySqlCommand($"SELECT COUNT(*) FROM {linked.Table} WHERE Banca=@code;", connection);
            count.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken)) > 0)
                return new(false, $"La banca non può essere eliminata: esistono {linked.Label} collegate.");
        }
        await using var command = new MySqlCommand("DELETE FROM Banche WHERE Codice=@code;", connection);
        command.Parameters.AddWithValue("@code", code);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1 ? new(true, "Banca eliminata.") : new(false, "Banca non trovata.");
    }

    private static void AddParameters(MySqlCommand command, BankEditModel bank)
    {
        command.Parameters.AddWithValue("@code", bank.Code); command.Parameters.AddWithValue("@name", bank.Name.Trim());
        command.Parameters.AddWithValue("@branch", Db(bank.Branch)); command.Parameters.AddWithValue("@website", Db(bank.Website));
        command.Parameters.AddWithValue("@email", Db(bank.Email)); command.Parameters.AddWithValue("@phone", Db(bank.Phone));
        command.Parameters.AddWithValue("@mobile", Db(bank.Mobile)); command.Parameters.AddWithValue("@abi", Db(bank.Abi));
        command.Parameters.AddWithValue("@cab", Db(bank.Cab)); command.Parameters.AddWithValue("@swift", Db(bank.Swift));
        command.Parameters.AddWithValue("@sia", Db(bank.Sia)); command.Parameters.AddWithValue("@account", Db(bank.Account));
        command.Parameters.AddWithValue("@iban", Db(bank.Iban)); command.Parameters.AddWithValue("@notes", Db(bank.Notes));
    }
    private static object Db(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    private static string Text(MySqlDataReader reader, string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? "" : Convert.ToString(reader[name]) ?? "";
}
