using System.Globalization;
using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Services;

public sealed class ActivityLogService(
    IHttpContextAccessor httpContextAccessor,
    Data.MicronoteDb database)
{
    public async Task LogCurrentUserAsync(
        string tableName,
        int action,
        string? recordKey = null,
        CancellationToken cancellationToken = default)
    {
        var userCode = CurrentUserCode();
        if (userCode <= 0)
        {
            return;
        }

        await LogForUserAsync(userCode, tableName, action, recordKey, cancellationToken);
    }

    public async Task LogForUserAsync(
        int userCode,
        string tableName,
        int action,
        string? recordKey = null,
        CancellationToken cancellationToken = default)
    {
        if (userCode <= 0 || string.IsNullOrWhiteSpace(tableName))
        {
            return;
        }

        try
        {
            var record = SplitRecordKey(recordKey);
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await EnsureSchemaAsync(connection, cancellationToken);

            await using var command = new MySqlCommand(
                """
                INSERT INTO Attivita
                (
                    TheDate,
                    TheTime,
                    Azienda,
                    Utente,
                    Tabella,
                    Azione,
                    Anno,
                    Numero,
                    Codice
                )
                VALUES
                (
                    CURDATE(),
                    CURTIME(),
                    @company,
                    @user,
                    @tableName,
                    @action,
                    @year,
                    @number,
                    @code
                );
                """,
                connection);
            command.Parameters.AddWithValue("@company", CurrentCompanyCode());
            command.Parameters.AddWithValue("@user", userCode);
            command.Parameters.AddWithValue("@tableName", tableName.Trim());
            command.Parameters.AddWithValue("@action", FormAzione.Base(action));
            command.Parameters.AddWithValue("@year", record.Year);
            command.Parameters.AddWithValue("@number", record.Number);
            command.Parameters.AddWithValue("@code", record.Code);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // Il registro attivita non deve mai bloccare l'operazione principale.
        }
    }

    public static string? RecordKey(int? code, int? year = null)
    {
        if (code is null)
        {
            return null;
        }

        return year is null
            ? code.Value.ToString(CultureInfo.InvariantCulture)
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{year.Value}/{code.Value}");
    }

    private async Task EnsureSchemaAsync(
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

    private int CurrentUserCode()
    {
        var value = httpContextAccessor.HttpContext?.Session.GetString(ApplicationAuthService.UserCodeKey);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
            ? code
            : 0;
    }

    private int CurrentCompanyCode()
    {
        var value = httpContextAccessor.HttpContext?.Session.GetString(ApplicationAuthService.CompanyCodeKey);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
            ? code
            : 0;
    }

    private static ActivityRecordKey SplitRecordKey(string? recordKey)
    {
        var code = recordKey?.Trim() ?? "";
        if (code.Length == 0)
        {
            return new ActivityRecordKey(0, "", "");
        }

        var parts = code
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        if (parts.Length >= 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var first)
            && parts[0].Length == 4)
        {
            return new ActivityRecordKey(first, string.Join('/', parts[1..]), code);
        }

        if (parts.Length >= 2
            && int.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var last)
            && parts[^1].Length == 4)
        {
            return new ActivityRecordKey(last, string.Join('/', parts[..^1]), code);
        }

        return new ActivityRecordKey(0, code, code);
    }

    private sealed record ActivityRecordKey(int Year, string Number, string Code);
}

