using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class ActivityRepository(MicronoteDb database)
{
    private static readonly ActivityActionOption[] Actions =
    [
        new(FormAzione.Inserimento, "Inserimento"),
        new(FormAzione.Modifica, "Modifica"),
        new(FormAzione.Cancellazione, "Cancellazione"),
        new(FormAzione.Stampa, "Stampa"),
        new(FormAzione.Login, "LOGIN"),
        new(FormAzione.Logout, "LOGOUT")
    ];

    public IReadOnlyList<ActivityActionOption> GetActionOptions() => Actions;

    public IReadOnlyList<int> GetYearOptions()
    {
        var currentYear = DateTime.Today.Year;
        return Enumerable.Range(0, 5).Select(offset => currentYear - offset).ToArray();
    }

    public async Task<IReadOnlyList<ActivityUserOption>> GetUserOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var command = new MySqlCommand(
            """
            SELECT DISTINCT
                a.Utente AS Codice,
                COALESCE(u.Username, '') AS Username,
                TRIM(CONCAT(COALESCE(u.Cognome, ''), ' ', COALESCE(u.Nome, ''))) AS FullName
            FROM Attivita a
            LEFT JOIN Utenti u ON u.Codice = a.Utente
            WHERE COALESCE(a.Utente, 0) <> 0
            ORDER BY FullName, Username, a.Utente;
            """,
            connection);

        var users = new List<ActivityUserOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var userName = Text(reader, "Username");
            var fullName = Text(reader, "FullName");
            var code = Convert.ToInt32(reader["Codice"]);
            users.Add(new ActivityUserOption(
                code,
                userName,
                string.IsNullOrWhiteSpace(fullName)
                    ? (string.IsNullOrWhiteSpace(userName) ? $"Utente {code:000}" : userName)
                    : fullName));
        }

        return users;
    }

    public async Task<IReadOnlyList<ActivityListItem>> SearchAsync(
        ActivitySearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        var conditions = new List<string>();
        if (filter.Year > 0)
        {
            conditions.Add("(YEAR(a.TheDate) = @filterYear OR a.TheDate IS NULL)");
        }

        if (filter.Month is >= 1 and <= 12)
        {
            conditions.Add("MONTH(a.TheDate) = @filterMonth");
        }

        if (filter.Day is >= 1 and <= 31)
        {
            conditions.Add("DAY(a.TheDate) = @filterDay");
        }

        if (filter.UserCode > 0)
        {
            conditions.Add("a.Utente = @filterUser");
        }

        if (filter.Action > 0)
        {
            conditions.Add("a.Azione = @filterAction");
        }

        var where = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : "";

        await using var command = new MySqlCommand(
            $"""
            SELECT
                0 AS ID,
                a.TheDate,
                a.TheTime,
                COALESCE(a.Azienda, 0) AS Azienda,
                COALESCE(a.Utente, 0) AS Utente,
                COALESCE(u.Username, '') AS Username,
                TRIM(CONCAT(COALESCE(u.Cognome, ''), ' ', COALESCE(u.Nome, ''))) AS FullName,
                COALESCE(a.Tabella, '') AS Tabella,
                COALESCE(a.Azione, 0) AS Azione,
                COALESCE(a.Anno, 0) AS Anno,
                COALESCE(a.Numero, '') AS Numero,
                COALESCE(a.Codice, '') AS Codice
            FROM Attivita a
            LEFT JOIN Utenti u ON u.Codice = a.Utente
            {where}
            ORDER BY a.TheDate DESC, a.TheTime DESC, a.Tabella, a.Numero, a.Codice
            LIMIT 1000;
            """,
            connection);
        command.Parameters.AddWithValue("@filterYear", filter.Year);
        command.Parameters.AddWithValue("@filterMonth", filter.Month);
        command.Parameters.AddWithValue("@filterDay", filter.Day);
        command.Parameters.AddWithValue("@filterUser", filter.UserCode);
        command.Parameters.AddWithValue("@filterAction", filter.Action);

        var rows = new List<ActivityListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var action = Convert.ToInt32(reader["Azione"]);
            var userName = Text(reader, "Username");
            var fullName = Text(reader, "FullName");
            rows.Add(new ActivityListItem(
                Convert.ToInt32(reader["ID"]),
                Date(reader, "TheDate"),
                Time(reader, "TheTime"),
                Convert.ToInt32(reader["Azienda"]),
                Convert.ToInt32(reader["Utente"]),
                userName,
                string.IsNullOrWhiteSpace(fullName)
                    ? (string.IsNullOrWhiteSpace(userName) ? $"Utente {Convert.ToInt32(reader["Utente"]):000}" : userName)
                    : fullName,
                Text(reader, "Tabella"),
                action,
                ActionDescription(action),
                Convert.ToInt32(reader["Anno"]),
                Text(reader, "Numero"),
                Text(reader, "Codice")));
        }

        return rows;
    }

    private static string ActionDescription(int action) =>
        Actions.FirstOrDefault(option => option.Code == action)?.Description ?? "";

    private static async Task EnsureSchemaAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            CREATE TABLE IF NOT EXISTS Attivita
            (
                TheDate DATE NULL,
                TheTime TIME NULL,
                Azienda SMALLINT NULL DEFAULT 0,
                Utente SMALLINT NULL DEFAULT 0,
                Tabella VARCHAR(50) NULL DEFAULT '',
                Azione TINYINT NULL DEFAULT 0,
                Anno SMALLINT NULL DEFAULT 0,
                Numero VARCHAR(30) NULL,
                Codice VARCHAR(30) NULL DEFAULT '',
                INDEX IX_Attivita_DataOra (TheDate DESC, TheTime DESC),
                INDEX IX_Attivita_Utente (Utente, TheDate DESC),
                INDEX IX_Attivita_TabellaRecord (Tabella, Codice)
            );
            """,
            connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Text(MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? "" : Convert.ToString(reader.GetValue(ordinal)) ?? "";
    }

    private static DateOnly? Date(MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return DateOnly.FromDateTime(Convert.ToDateTime(reader.GetValue(ordinal)));
    }

    private static TimeOnly? Time(MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return TimeOnly.FromTimeSpan((TimeSpan)reader.GetValue(ordinal));
    }
}


