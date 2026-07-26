using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class AccountingCauseRepository(
    MicronoteDb database,
    ProgressiveCodeService progressiveCodes)
{
    public static IReadOnlyList<AccountingCauseOption> MovementTypeOptions { get; } =
    [
        new("V", "Movimento IVA"),
        new("C", "Movimento contabile"),
        new("B", "Movimento bancario"),
        new("D", "Movimento dipendenti")
    ];

    public static IReadOnlyList<AccountingCauseOption> SubjectOptions { get; } =
    [
        new("C", "Cliente"),
        new("F", "Fornitore"),
        new("B", "Banca"),
        new("D", "Dipendente")
    ];

    public static IReadOnlyList<AccountingCauseOption> SignOptions { get; } =
    [
        new("D", "Dare"),
        new("A", "Avere")
    ];

    public static IReadOnlyList<AccountingCauseOption> CashFlowOptions { get; } =
    [
        new("E", "Entrata"),
        new("U", "Uscita")
    ];

    public async Task<IReadOnlyList<AccountingCauseListItem>> ListAsync(
        string? movementType,
        string? search,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT
                cc.Codice,
                COALESCE(cc.Descrizione, '') AS Descrizione,
                COALESCE(cc.TipoMov, '') AS TipoMov,
                COALESCE(cc.CliFor, '') AS CliFor,
                COALESCE(cc.Segno, '') AS Segno,
                COALESCE(cc.EnUs, '') AS EnUs,
                COALESCE(cc.Dare1, 0) AS Dare1,
                COALESCE(d.Descrizione, '') AS DareDescrizione,
                COALESCE(cc.Avere1, 0) AS Avere1,
                COALESCE(a.Descrizione, '') AS AvereDescrizione,
                COALESCE(cc.Cassa, 0) AS Cassa,
                COALESCE(cc.Fattura, 0) AS Fattura,
                COALESCE(cc.Scadenza, 0) AS Scadenza,
                COALESCE(cc.Titolo, 0) AS Titolo,
                COALESCE(cc.Stipendio, 0) AS Stipendio,
                COALESCE(cc.Stampa, 0) AS Stampa,
                COALESCE(cc.Locked, 0) AS Locked
            FROM causalicont cc
            LEFT JOIN conti d ON d.Codice = cc.Dare1
            LEFT JOIN conti a ON a.Codice = cc.Avere1
            WHERE (@movementType = '' OR cc.TipoMov = @movementType)
              AND (@search = '' OR cc.Descrizione LIKE @searchLike)
            ORDER BY cc.Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        AddFilterParameters(command, movementType, search);

        var causes = new List<AccountingCauseListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            causes.Add(new AccountingCauseListItem(
                Convert.ToInt32(reader["Codice"]),
                reader.GetString("Descrizione"),
                reader.GetString("TipoMov"),
                reader.GetString("CliFor"),
                reader.GetString("Segno"),
                reader.GetString("EnUs"),
                Convert.ToInt32(reader["Dare1"]),
                reader.GetString("DareDescrizione"),
                Convert.ToInt32(reader["Avere1"]),
                reader.GetString("AvereDescrizione"),
                ToBool(reader["Cassa"]),
                ToBool(reader["Fattura"]),
                ToBool(reader["Scadenza"]),
                ToBool(reader["Titolo"]),
                ToBool(reader["Stipendio"]),
                ToBool(reader["Stampa"]),
                ToBool(reader["Locked"])));
        }

        return causes;
    }

    public async Task<AccountingCauseEditModel?> GetAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Descrizione, '') AS Descrizione,
                COALESCE(TipoMov, '') AS TipoMov,
                COALESCE(CliFor, '') AS CliFor,
                COALESCE(Segno, '') AS Segno,
                COALESCE(EnUs, '') AS EnUs,
                COALESCE(Cassa, 0) AS Cassa,
                COALESCE(Fattura, 0) AS Fattura,
                COALESCE(Scadenza, 0) AS Scadenza,
                COALESCE(Titolo, 0) AS Titolo,
                COALESCE(Stipendio, 0) AS Stipendio,
                COALESCE(Stampa, 0) AS Stampa,
                COALESCE(Locked, 0) AS Locked,
                COALESCE(Dare1, 0) AS Dare1,
                COALESCE(Dare2, 0) AS Dare2,
                COALESCE(Dare3, 0) AS Dare3,
                COALESCE(Dare4, 0) AS Dare4,
                COALESCE(Dare5, 0) AS Dare5,
                COALESCE(Dare6, 0) AS Dare6,
                COALESCE(Avere1, 0) AS Avere1,
                COALESCE(Avere2, 0) AS Avere2,
                COALESCE(Avere3, 0) AS Avere3,
                COALESCE(Avere4, 0) AS Avere4,
                COALESCE(Avere5, 0) AS Avere5,
                COALESCE(Avere6, 0) AS Avere6
            FROM causalicont
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

        return new AccountingCauseEditModel
        {
            Code = Convert.ToInt32(reader["Codice"]),
            Description = reader.GetString("Descrizione"),
            MovementType = reader.GetString("TipoMov"),
            Subject = reader.GetString("CliFor"),
            Sign = reader.GetString("Segno"),
            CashFlow = reader.GetString("EnUs"),
            Cash = ToBool(reader["Cassa"]),
            Invoice = ToBool(reader["Fattura"]),
            DueDate = ToBool(reader["Scadenza"]),
            Title = ToBool(reader["Titolo"]),
            Salary = ToBool(reader["Stipendio"]),
            Print = ToBool(reader["Stampa"]),
            Locked = ToBool(reader["Locked"]),
            Debit1 = ToNullableCode(reader["Dare1"]),
            Debit2 = ToNullableCode(reader["Dare2"]),
            Debit3 = ToNullableCode(reader["Dare3"]),
            Debit4 = ToNullableCode(reader["Dare4"]),
            Debit5 = ToNullableCode(reader["Dare5"]),
            Debit6 = ToNullableCode(reader["Dare6"]),
            Credit1 = ToNullableCode(reader["Avere1"]),
            Credit2 = ToNullableCode(reader["Avere2"]),
            Credit3 = ToNullableCode(reader["Avere3"]),
            Credit4 = ToNullableCode(reader["Avere4"]),
            Credit5 = ToNullableCode(reader["Avere5"]),
            Credit6 = ToNullableCode(reader["Avere6"])
        };
    }

    public async Task<IReadOnlyList<AccountingCauseAccountOption>> GetAccountOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.Codice,
                COALESCE(c.Descrizione, '') AS Descrizione,
                COALESCE(c.Mastro, 0) AS Mastro,
                COALESCE(m.Descrizione, '') AS MastroDescrizione,
                COALESCE(c.Tipo, '') AS Tipo
            FROM conti c
            LEFT JOIN mastri m ON m.Codice = c.Mastro
            ORDER BY c.Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var options = new List<AccountingCauseAccountOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = Convert.ToInt32(reader["Codice"]);
            options.Add(new AccountingCauseAccountOption(
                code,
                reader.GetString("Descrizione"),
                Convert.ToInt32(reader["Mastro"]),
                reader.GetString("MastroDescrizione"),
                reader.GetString("Tipo").Trim().ToUpperInvariant()));
        }

        return options;
    }

    public Task<int> NextCodeAsync(CancellationToken cancellationToken = default) =>
        progressiveCodes.NextCodeAsync(
            "causalicont",
            "Codice",
            cancellationToken: cancellationToken);

    public async Task<bool> ExistsAsync(int code, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM causalicont WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task InsertAsync(
        AccountingCauseEditModel cause,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO causalicont
                (Codice, Descrizione, TipoMov, CliFor, Segno, EnUs,
                 Cassa, Fattura, Scadenza, Titolo, Stipendio, Stampa, Locked,
                 Dare1, Dare2, Dare3, Dare4, Dare5, Dare6,
                 Avere1, Avere2, Avere3, Avere4, Avere5, Avere6)
            VALUES
                (@code, @description, @movementType, @subject, @sign, @cashFlow,
                 @cash, @invoice, @dueDate, @title, @salary, @print, @locked,
                 @debit1, @debit2, @debit3, @debit4, @debit5, @debit6,
                 @credit1, @credit2, @credit3, @credit4, @credit5, @credit6);
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        cause.Code = await progressiveCodes.NextCodeAsync(
            connection,
            "causalicont",
            "Codice",
            cancellationToken: cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, cause);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(
        AccountingCauseEditModel cause,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE causalicont
            SET
                Descrizione = @description,
                TipoMov = @movementType,
                CliFor = @subject,
                Segno = @sign,
                EnUs = @cashFlow,
                Cassa = @cash,
                Fattura = @invoice,
                Scadenza = @dueDate,
                Titolo = @title,
                Stipendio = @salary,
                Stampa = @print,
                Locked = @locked,
                Dare1 = @debit1,
                Dare2 = @debit2,
                Dare3 = @debit3,
                Dare4 = @debit4,
                Dare5 = @debit5,
                Dare6 = @debit6,
                Avere1 = @credit1,
                Avere2 = @credit2,
                Avere3 = @credit3,
                Avere4 = @credit4,
                Avere5 = @credit5,
                Avere6 = @credit6
            WHERE Codice = @code;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, cause);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<AccountingCauseDeleteResult> DeleteAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        await using (var lockedCommand = new MySqlCommand(
            "SELECT COALESCE(Locked, 0) FROM causalicont WHERE Codice = @code LIMIT 1;",
            connection))
        {
            lockedCommand.Parameters.AddWithValue("@code", code);
            var locked = await lockedCommand.ExecuteScalarAsync(cancellationToken);
            if (locked is null)
            {
                return new AccountingCauseDeleteResult(false, "Causale non trovata.");
            }

            if (Convert.ToInt32(locked) != 0)
            {
                return new AccountingCauseDeleteResult(
                    false,
                    "La causale non può essere eliminata perché è bloccata.");
            }
        }

        if (await ColumnExistsAsync(connection, "movcont", "Causale", cancellationToken))
        {
            await using var linkedCommand = new MySqlCommand(
                "SELECT COUNT(*) FROM movcont WHERE Causale = @code;",
                connection);
            linkedCommand.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(await linkedCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return new AccountingCauseDeleteResult(
                    false,
                    "La causale non può essere eliminata: esistono movimenti collegati.");
            }
        }

        await using var command = new MySqlCommand(
            "DELETE FROM causalicont WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? new AccountingCauseDeleteResult(true, "Causale eliminata.")
            : new AccountingCauseDeleteResult(false, "Causale non trovata.");
    }

    private static void AddFilterParameters(
        MySqlCommand command,
        string? movementType,
        string? search)
    {
        var normalizedMovementType = NormalizeCode(movementType);
        var normalizedSearch = search?.Trim() ?? "";

        command.Parameters.AddWithValue("@movementType", normalizedMovementType);
        command.Parameters.AddWithValue("@search", normalizedSearch);
        command.Parameters.AddWithValue("@searchLike", $"%{normalizedSearch}%");
    }

    private static void AddSaveParameters(MySqlCommand command, AccountingCauseEditModel cause)
    {
        command.Parameters.AddWithValue("@code", cause.Code);
        command.Parameters.AddWithValue("@description", cause.Description.Trim());
        command.Parameters.AddWithValue("@movementType", NormalizeCode(cause.MovementType));
        command.Parameters.AddWithValue("@subject", NormalizeCode(cause.Subject));
        command.Parameters.AddWithValue("@sign", NormalizeCode(cause.Sign));
        command.Parameters.AddWithValue("@cashFlow", NormalizeCode(cause.CashFlow));
        command.Parameters.AddWithValue("@cash", cause.Cash ? 1 : 0);
        command.Parameters.AddWithValue("@invoice", cause.Invoice ? 1 : 0);
        command.Parameters.AddWithValue("@dueDate", cause.DueDate ? 1 : 0);
        command.Parameters.AddWithValue("@title", cause.Title ? 1 : 0);
        command.Parameters.AddWithValue("@salary", cause.Salary ? 1 : 0);
        command.Parameters.AddWithValue("@print", cause.Print ? 1 : 0);
        command.Parameters.AddWithValue("@locked", cause.Locked ? 1 : 0);
        command.Parameters.AddWithValue("@debit1", ToSaveCode(cause.Debit1));
        command.Parameters.AddWithValue("@debit2", ToSaveCode(cause.Debit2));
        command.Parameters.AddWithValue("@debit3", ToSaveCode(cause.Debit3));
        command.Parameters.AddWithValue("@debit4", ToSaveCode(cause.Debit4));
        command.Parameters.AddWithValue("@debit5", ToSaveCode(cause.Debit5));
        command.Parameters.AddWithValue("@debit6", ToSaveCode(cause.Debit6));
        command.Parameters.AddWithValue("@credit1", ToSaveCode(cause.Credit1));
        command.Parameters.AddWithValue("@credit2", ToSaveCode(cause.Credit2));
        command.Parameters.AddWithValue("@credit3", ToSaveCode(cause.Credit3));
        command.Parameters.AddWithValue("@credit4", ToSaveCode(cause.Credit4));
        command.Parameters.AddWithValue("@credit5", ToSaveCode(cause.Credit5));
        command.Parameters.AddWithValue("@credit6", ToSaveCode(cause.Credit6));
    }

    private static async Task<bool> ColumnExistsAsync(
        MySqlConnection connection,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = @tableName
              AND column_name = @columnName;
            """,
            connection);
        command.Parameters.AddWithValue("@tableName", tableName);
        command.Parameters.AddWithValue("@columnName", columnName);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static bool ToBool(object value) => Convert.ToInt32(value) != 0;

    private static int? ToNullableCode(object value)
    {
        var code = Convert.ToInt32(value);
        return code > 0 ? code : null;
    }

    private static int ToSaveCode(int? value) => value.GetValueOrDefault() > 0 ? value.GetValueOrDefault() : 0;

    private static string NormalizeCode(string? value) =>
        value?.Trim().ToUpperInvariant() ?? "";
}
