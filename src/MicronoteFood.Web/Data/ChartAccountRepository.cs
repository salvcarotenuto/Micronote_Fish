using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class ChartAccountRepository(
    MicronoteDb database,
    ProgressiveCodeService progressiveCodes)
{
    public static IReadOnlyList<ChartAccountTypeOption> TypeOptions { get; } =
    [
        new("P", "Patrimoniale"),
        new("C", "Costo"),
        new("R", "Ricavo")
    ];

    public static IReadOnlyList<ChartAccountCompanyTypeOption> CompanyTypeOptions { get; } =
    [
        new("C", "Cliente"),
        new("F", "Fornitore"),
        new("B", "Banca"),
        new("D", "Dipendente")
    ];

    public async Task<IReadOnlyList<ChartAccountListItem>> ListAsync(
        int? masterCode,
        string? type,
        string? search,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.Codice,
                COALESCE(c.Descrizione, '') AS Descrizione,
                COALESCE(c.Mastro, 0) AS Mastro,
                COALESCE(m.Descrizione, '') AS MastroDescrizione,
                COALESCE(m.Tipo, c.Tipo, '') AS Tipo,
                COALESCE(c.Ditta, '') AS Ditta,
                COALESCE(c.Carico, 0) AS Carico,
                COALESCE(c.Locked, 0) AS Locked
            FROM conti c
            LEFT JOIN mastri m ON m.Codice = c.Mastro
            WHERE (@masterCode IS NULL OR c.Mastro = @masterCode)
              AND (@type = '' OR m.Tipo = @type)
              AND (@search = '' OR c.Descrizione LIKE @searchLike)
            ORDER BY c.Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        AddFilterParameters(command, masterCode, type, search);

        var accounts = new List<ChartAccountListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(new ChartAccountListItem(
                Convert.ToInt32(reader["Codice"]),
                reader.GetString("Descrizione"),
                Convert.ToInt32(reader["Mastro"]),
                reader.GetString("MastroDescrizione"),
                NormalizeType(reader.GetString("Tipo")),
                reader.GetString("Ditta"),
                Convert.ToInt32(reader["Carico"]) != 0,
                Convert.ToInt32(reader["Locked"]) != 0));
        }

        return accounts;
    }

    public async Task<ChartAccountEditModel?> GetAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.Codice,
                COALESCE(c.Descrizione, '') AS Descrizione,
                COALESCE(c.Mastro, 0) AS Mastro,
                COALESCE(m.Tipo, c.Tipo, '') AS Tipo,
                COALESCE(c.Ditta, '') AS Ditta,
                COALESCE(c.Carico, 0) AS Carico,
                COALESCE(c.Locked, 0) AS Locked
            FROM conti c
            LEFT JOIN mastri m ON m.Codice = c.Mastro
            WHERE c.Codice = @code
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

        return new ChartAccountEditModel
        {
            IsNew = false,
            Code = Convert.ToInt32(reader["Codice"]),
            Description = reader.GetString("Descrizione"),
            MasterCode = Convert.ToInt32(reader["Mastro"]),
            Type = TypeDescription(reader.GetString("Tipo")),
            CompanyType = reader.GetString("Ditta"),
            StockLoad = Convert.ToInt32(reader["Carico"]) != 0,
            Locked = Convert.ToInt32(reader["Locked"]) != 0
        };
    }

    public async Task<IReadOnlyList<LookupOption>> GetMasterOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione
            FROM mastri
            ORDER BY Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var options = new List<LookupOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = Convert.ToInt32(reader["Codice"]);
            options.Add(new LookupOption(
                code,
                $"{code:000} - {reader.GetString("Descrizione")}"));
        }

        return options;
    }

    public async Task<IReadOnlyDictionary<int, string>> GetMasterTypesAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Codice, COALESCE(Tipo, '') AS Tipo
            FROM mastri;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var types = new Dictionary<int, string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            types[Convert.ToInt32(reader["Codice"])] = TypeDescription(reader.GetString("Tipo"));
        }

        return types;
    }

    public async Task<string> MasterTypeAsync(
        int masterCode,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COALESCE(Tipo, '') FROM mastri WHERE Codice = @masterCode LIMIT 1;",
            connection);
        command.Parameters.AddWithValue("@masterCode", masterCode);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? "" : NormalizeType(Convert.ToString(result));
    }

    public async Task<bool> ExistsAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM conti WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public Task<int> NextCodeAsync(CancellationToken cancellationToken = default) =>
        progressiveCodes.NextCodeAsync(
            "conti",
            "Codice",
            cancellationToken: cancellationToken);

    public async Task InsertAsync(
        ChartAccountEditModel account,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO conti
                (Codice, Descrizione, Mastro, Tipo, Ditta, Carico, Locked)
            VALUES
                (@code, @description, @masterCode,
                 (SELECT COALESCE(Tipo, '') FROM mastri WHERE Codice = @masterCode LIMIT 1),
                 @companyType, @stockLoad, @locked);
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        account.Code = await progressiveCodes.NextCodeAsync(
            connection,
            "conti",
            "Codice",
            cancellationToken: cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, account);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(
        ChartAccountEditModel account,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE conti
            SET
                Descrizione = @description,
                Mastro = @masterCode,
                Tipo = (SELECT COALESCE(Tipo, '') FROM mastri WHERE Codice = @masterCode LIMIT 1),
                Ditta = @companyType,
                Carico = @stockLoad,
                Locked = @locked
            WHERE Codice = @code;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, account);

        if (await command.ExecuteNonQueryAsync(cancellationToken) == 1)
        {
            return true;
        }

        await using var existsCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM conti WHERE Codice = @code;",
            connection);
        existsCommand.Parameters.AddWithValue("@code", account.Code);
        return Convert.ToInt32(await existsCommand.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task<ChartAccountDeleteResult> DeleteAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        await using (var lockedCommand = new MySqlCommand(
            "SELECT COALESCE(Locked, 0) FROM conti WHERE Codice = @code LIMIT 1;",
            connection))
        {
            lockedCommand.Parameters.AddWithValue("@code", code);
            var locked = lockedCommand.ExecuteScalar();
            if (locked is null)
            {
                return new ChartAccountDeleteResult(false, "Conto non trovato.");
            }

            if (Convert.ToInt32(locked) != 0)
            {
                return new ChartAccountDeleteResult(
                    false,
                    "Il conto non può essere eliminato perché è bloccato.");
            }
        }

        await using (var linkedCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM movcontrg WHERE Conto = @code;",
            connection))
        {
            linkedCommand.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(await linkedCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return new ChartAccountDeleteResult(
                    false,
                    "Il conto non può essere eliminato: esistono movimenti collegati.");
            }
        }

        await using var command = new MySqlCommand(
            "DELETE FROM conti WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? new ChartAccountDeleteResult(true, "Conto eliminato.")
            : new ChartAccountDeleteResult(false, "Conto non trovato.");
    }

    private static void AddFilterParameters(
        MySqlCommand command,
        int? masterCode,
        string? type,
        string? search)
    {
        var normalizedType = NormalizeType(type);
        var normalizedSearch = search?.Trim() ?? "";

        command.Parameters.AddWithValue(
            "@masterCode",
            masterCode.HasValue ? masterCode.Value : DBNull.Value);
        command.Parameters.AddWithValue("@type", normalizedType);
        command.Parameters.AddWithValue("@search", normalizedSearch);
        command.Parameters.AddWithValue("@searchLike", $"%{normalizedSearch}%");
    }

    private static void AddSaveParameters(MySqlCommand command, ChartAccountEditModel account)
    {
        command.Parameters.AddWithValue("@code", account.Code);
        command.Parameters.AddWithValue("@description", account.Description.Trim());
        command.Parameters.AddWithValue("@masterCode", account.MasterCode);
        command.Parameters.AddWithValue(
            "@companyType",
            account.CompanyType?.Trim().ToUpperInvariant() ?? "");
        command.Parameters.AddWithValue("@stockLoad", account.StockLoad ? 1 : 0);
        command.Parameters.AddWithValue("@locked", account.Locked ? 1 : 0);
    }

    private static string NormalizeType(string? type)
    {
        var normalized = type?.Trim().ToUpperInvariant() ?? "";
        return normalized;
    }

    public static string TypeDescription(string? type)
    {
        var normalized = NormalizeType(type);
        return TypeOptions.FirstOrDefault(option => option.Code == normalized)?.Description ?? "";
    }
}
