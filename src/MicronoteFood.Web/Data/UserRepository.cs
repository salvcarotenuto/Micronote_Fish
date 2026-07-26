using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class UserRepository(
    MicronoteDb database,
    ProgressiveCodeService progressiveCodes)
{
    private static readonly LookupOption[] UserTypes =
    [
        new(1, "Assistenza"),
        new(2, "Amministratore del sistema"),
        new(3, "Utente normale"),
        new(4, "Operatore esterno")
    ];

    private static readonly LookupOption[] Qualifications =
    [
        new(1, "Titolare azienda"),
        new(2, "Dirigente azienda"),
        new(3, "Impiegato amministrativo"),
        new(4, "Impiegato tecnico"),
        new(5, "Agente"),
        new(6, "Cassiere"),
        new(7, "Esterno all'azienda")
    ];

    private static readonly LookupOption[] Genders =
    [
        new(1, "Maschile"),
        new(2, "Femminile")
    ];

    public async Task<IReadOnlyList<UserListItem>> SearchAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Cognome, '') AS Cognome,
                COALESCE(Nome, '') AS Nome,
                COALESCE(Username, '') AS Username,
                COALESCE(Citta, '') AS Citta,
                COALESCE(Telefono, '') AS Telefono,
                COALESCE(Email, '') AS Email,
                COALESCE(Bloccato, 0) AS Bloccato,
                COALESCE(Attivo, 1) AS Attivo,
                COALESCE(Tipo, 0) AS Tipo
            FROM Utenti
            WHERE
                @search = ''
                OR Cognome LIKE CONCAT('%', @search, '%')
                OR Nome LIKE CONCAT('%', @search, '%')
                OR Username LIKE CONCAT('%', @search, '%')
            ORDER BY Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@search", search?.Trim() ?? "");

        var users = new List<UserListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var typeCode = Convert.ToInt32(reader["Tipo"]);
            users.Add(new UserListItem(
                reader.GetInt32("Codice"),
                reader.GetString("Cognome"),
                reader.GetString("Nome"),
                reader.GetString("Username"),
                reader.GetString("Citta"),
                reader.GetString("Telefono"),
                reader.GetString("Email"),
                Convert.ToBoolean(reader["Bloccato"]),
                Convert.ToBoolean(reader["Attivo"]),
                typeCode,
                TypeDescription(typeCode)));
        }

        return users;
    }

    public async Task<UserEditModel?> GetAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT *
            FROM Utenti
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

        return new UserEditModel
        {
            Code = reader.GetInt32("Codice"),
            LastName = Text(reader, "Cognome") ?? "",
            FirstName = Text(reader, "Nome") ?? "",
            City = Text(reader, "Citta"),
            Address = Text(reader, "Indirizzo"),
            TaxCode = Text(reader, "CodFi"),
            Phone = Text(reader, "Telefono"),
            Email = Text(reader, "Email"),
            UserName = Text(reader, "Username") ?? "",
            Password = Text(reader, "Passwd") ?? "",
            TypeCode = Integer(reader, "Tipo"),
            QualificationCode = Integer(reader, "Qualifica"),
            StoreCode = Integer(reader, "PuntoV"),
            Gender = Text(reader, "Sesso"),
            IsActive = Boolean(reader, "Attivo", true),
            IsLocked = Boolean(reader, "Bloccato", false)
        };
    }


    public async Task<IReadOnlyList<UserLoginOption>> GetLoginOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Username, '') AS Username,
                TRIM(CONCAT(COALESCE(Cognome, ''), ' ', COALESCE(Nome, ''))) AS FullName
            FROM Utenti
            ORDER BY Cognome, Nome, Username;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var users = new List<UserLoginOption>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var userName = reader.GetString("Username");
            var fullName = reader.GetString("FullName");
            var displayName = string.IsNullOrWhiteSpace(fullName)
                ? userName
                : fullName;

            users.Add(new UserLoginOption(
                reader.GetInt32("Codice"),
                userName,
                displayName));
        }

        return users;
    }

    public async Task<UserLoginResult> CheckLoginAsync(
        int code,
        string password,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Username, '') AS Username,
                COALESCE(Passwd, '') AS Passwd,
                TRIM(CONCAT(COALESCE(Cognome, ''), ' ', COALESCE(Nome, ''))) AS FullName,
                COALESCE(Attivo, 1) AS Attivo,
                COALESCE(Bloccato, 0) AS Bloccato
            FROM Utenti
            WHERE Codice = @code
            LIMIT 1;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new UserLoginResult(
                UserLoginStatus.UserNotFound,
                null,
                "Utente non trovato.");
        }

        var userName = reader.GetString("Username");
        var fullName = reader.GetString("FullName");
        var user = new UserLoginOption(
            reader.GetInt32("Codice"),
            userName,
            string.IsNullOrWhiteSpace(fullName) ? userName : fullName);

        if (!Convert.ToBoolean(reader["Attivo"]))
        {
            return new UserLoginResult(
                UserLoginStatus.UserInactive,
                user,
                "L'utente selezionato non risulta attivo.");
        }

        if (Convert.ToBoolean(reader["Bloccato"]))
        {
            return new UserLoginResult(
                UserLoginStatus.UserLocked,
                user,
                "L'utente selezionato risulta bloccato.");
        }

        var savedPassword = reader.GetString("Passwd");
        if (!string.Equals(savedPassword, password, StringComparison.OrdinalIgnoreCase))
        {
            return new UserLoginResult(
                UserLoginStatus.InvalidPassword,
                user,
                "Password utente non valida.");
        }

        return new UserLoginResult(
            UserLoginStatus.Success,
            user,
            "");
    }
    public async Task<UserLookups> GetLookupsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        return new UserLookups(
            await LoadLookupAsync(
                connection,
                "SELECT Codice, Nome FROM puntivendita ORDER BY Nome",
                cancellationToken),
            UserTypes,
            Qualifications,
            Genders);
    }

    public async Task<int> InsertAsync(
        UserEditModel user,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        user.Code = await progressiveCodes.NextCodeAsync(
            connection,
            "Utenti",
            "Codice",
            cancellationToken: cancellationToken);

        if (await UserNameExistsAsync(connection, user.Code, user.UserName, cancellationToken))
        {
            throw new InvalidOperationException("Username già presente.");
        }

        const string sql = """
            INSERT INTO Utenti
            (
                Codice, Cognome, Nome, Citta, Indirizzo, CodFi,
                Telefono, Email, Attivo, Username, Passwd, Bloccato,
                Tipo, Qualifica, PuntoV, Sesso
            )
            VALUES
            (
                @code, @lastName, @firstName, @city, @address, @taxCode,
                @phone, @email, @isActive, @userName, @password, @isLocked,
                @typeCode, @qualificationCode, @storeCode, @gender
            );
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, user);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return user.Code;
    }

    public Task<int> NextCodeAsync(CancellationToken cancellationToken = default) =>
        progressiveCodes.NextCodeAsync(
            "Utenti",
            "Codice",
            cancellationToken: cancellationToken);

    public async Task<bool> UpdateAsync(
        UserEditModel user,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        await using (var existsCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM Utenti WHERE Codice = @code;",
            connection))
        {
            existsCommand.Parameters.AddWithValue("@code", user.Code);
            if (Convert.ToInt32(
                await existsCommand.ExecuteScalarAsync(cancellationToken)) != 1)
            {
                return false;
            }
        }

        if (await UserNameExistsAsync(connection, user.Code, user.UserName, cancellationToken))
        {
            throw new InvalidOperationException("Username già presente.");
        }

        const string sql = """
            UPDATE Utenti
            SET
                Cognome = @lastName,
                Nome = @firstName,
                Citta = @city,
                Indirizzo = @address,
                CodFi = @taxCode,
                Telefono = @phone,
                Email = @email,
                Attivo = @isActive,
                Username = @userName,
                Passwd = @password,
                Bloccato = @isLocked,
                Tipo = @typeCode,
                Qualifica = @qualificationCode,
                PuntoV = @storeCode,
                Sesso = @gender
            WHERE Codice = @code;
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, user);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    public async Task<UserDeleteResult> DeleteAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        await using (var typeCommand = new MySqlCommand(
            "SELECT COALESCE(Tipo, 0) FROM Utenti WHERE Codice = @code;",
            connection))
        {
            typeCommand.Parameters.AddWithValue("@code", code);
            var type = await typeCommand.ExecuteScalarAsync(cancellationToken);
            if (type is null)
            {
                return new UserDeleteResult(false, "Utente non trovato.");
            }

            if (Convert.ToInt32(type) == 1)
            {
                return new UserDeleteResult(
                    false,
                    "L'utente di assistenza non può essere eliminato.");
            }
        }

        await using (var activityCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM Attivita WHERE Utente = @code;",
            connection))
        {
            activityCommand.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(
                await activityCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return new UserDeleteResult(
                    false,
                    "L'utente non può essere eliminato: esistono attività collegate.");
            }
        }

        await using var deleteCommand = new MySqlCommand(
            "DELETE FROM Utenti WHERE Codice = @code;",
            connection);
        deleteCommand.Parameters.AddWithValue("@code", code);
        var affectedRows = await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

        return affectedRows == 1
            ? new UserDeleteResult(true, "Utente eliminato.")
            : new UserDeleteResult(false, "Utente non trovato.");
    }

    private static async Task<bool> UserNameExistsAsync(
        MySqlConnection connection,
        int code,
        string userName,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM Utenti WHERE Username = @userName AND Codice <> @code;",
            connection);
        command.Parameters.AddWithValue("@userName", userName.Trim());
        command.Parameters.AddWithValue("@code", code);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static void AddSaveParameters(MySqlCommand command, UserEditModel user)
    {
        command.Parameters.AddWithValue("@code", user.Code);
        command.Parameters.AddWithValue("@lastName", Clean(user.LastName));
        command.Parameters.AddWithValue("@firstName", Clean(user.FirstName));
        command.Parameters.AddWithValue("@city", DbText(user.City));
        command.Parameters.AddWithValue("@address", DbText(user.Address));
        command.Parameters.AddWithValue("@taxCode", DbText(user.TaxCode));
        command.Parameters.AddWithValue("@phone", DbText(user.Phone));
        command.Parameters.AddWithValue("@email", DbText(user.Email));
        command.Parameters.AddWithValue("@isActive", user.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@userName", Clean(user.UserName));
        command.Parameters.AddWithValue("@password", Clean(user.Password));
        command.Parameters.AddWithValue("@isLocked", user.IsLocked ? 1 : 0);
        command.Parameters.AddWithValue("@typeCode", user.TypeCode ?? 0);
        command.Parameters.AddWithValue(
            "@qualificationCode",
            DbNumber(user.QualificationCode));
        command.Parameters.AddWithValue("@storeCode", DbNumber(user.StoreCode));
        command.Parameters.AddWithValue("@gender", DbText(user.Gender));
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

    private static string TypeDescription(int typeCode) =>
        UserTypes.FirstOrDefault(type => type.Code == typeCode)?.Description ?? "";

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


