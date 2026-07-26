using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class AccountingStatementRepository(MicronoteDb database)
{
    public async Task<AccountingStatementPageModel> GetInitialAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        return new AccountingStatementPageModel
        {
            DateFrom = new DateOnly(year, 1, 1),
            DateTo = DefaultDateTo(year),
            Accounts = await ListAccountsAsync(cancellationToken)
        };
    }

    public async Task<AccountingStatementPageModel> GetPartyInitialAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        return new AccountingStatementPageModel
        {
            StatementMode = "party",
            PageTitle = "Estratto conto clienti e fornitori",
            PageDescription = "Visualizzazione e stampa estratto conto clienti e fornitori",
            PartyType = "F",
            DateFrom = new DateOnly(year, 1, 1),
            DateTo = DefaultDateTo(year),
            Parties = await ListPartiesAsync(cancellationToken)
        };
    }

    public async Task<AccountingStatementPageModel> GetPartyAsync(
        string partyType,
        int partyCode,
        DateOnly dateFrom,
        DateOnly dateTo,
        CancellationToken cancellationToken = default)
    {
        partyType = NormalizePartyType(partyType);
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var party = await FindPartyAsync(connection, partyType, partyCode, cancellationToken);
        var rows = new List<AccountingStatementRow>();
        var balance = await PartyInitialBalanceAsync(connection, dateFrom.Year, partyType, partyCode, cancellationToken);
        var dateStart = new DateOnly(dateFrom.Year, 1, 1);
        var dateEnd = dateFrom.AddDays(-1);

        if (dateEnd >= dateStart)
        {
            balance += await PartyMovementBalanceAsync(
                connection,
                partyType,
                partyCode,
                dateStart,
                dateEnd,
                cancellationToken);
        }

        var totalDebit = balance > 0 ? balance : 0m;
        var totalCredit = balance < 0 ? Math.Abs(balance) : 0m;

        rows.Add(new AccountingStatementRow(
            null,
            null,
            null,
            null,
            null,
            "",
            null,
            "Saldo iniziale",
            null,
            "",
            balance > 0 ? balance : 0m,
            balance < 0 ? Math.Abs(balance) : 0m,
            balance,
            balance < 0 ? "A" : "D"));

        const string sql = """
            SELECT m.ID,
                   m.Anno,
                   m.Settore,
                   m.Codice,
                   m.DataMov,
                   COALESCE(m.NumDoc, '') AS NumDoc,
                   COALESCE(m.Causale, 0) AS Causale,
                   COALESCE(m.Importo, 0) AS Importo,
                   COALESCE(c.Descrizione, '') AS DecCausale,
                   COALESCE(c.Segno, '') AS Segno,
                   COALESCE((
                       SELECT rg.Conto
                       FROM movcontrg rg
                       WHERE rg.ID = m.ID
                         AND rg.Conto NOT IN (81, 82)
                       ORDER BY rg.Riga
                       LIMIT 1
                   ), 0) AS CtPartita,
                   COALESCE((
                       SELECT ct.Descrizione
                       FROM movcontrg rg
                       LEFT JOIN conti ct ON ct.Codice = rg.Conto
                       WHERE rg.ID = m.ID
                         AND rg.Conto NOT IN (81, 82)
                       ORDER BY rg.Riga
                       LIMIT 1
                   ), '') AS DecPartita
            FROM movcont m
            LEFT JOIN causalicont c ON c.Codice = m.Causale
            WHERE m.CliFor = @partyType
              AND m.Ditta = @partyCode
              AND m.DataMov BETWEEN @dateFrom AND @dateTo
            ORDER BY m.DataMov, m.Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@partyType", MySqlDbType.VarChar).Value = partyType;
        command.Parameters.Add("@partyCode", MySqlDbType.Int32).Value = partyCode;
        command.Parameters.Add("@dateFrom", MySqlDbType.Date).Value = dateFrom.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@dateTo", MySqlDbType.Date).Value = dateTo.ToDateTime(TimeOnly.MinValue);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var sign = Convert.ToString(reader["Segno"]) ?? "";
            var amount = Money(reader["Importo"]);
            var debit = sign == "D" ? amount : 0m;
            var credit = sign == "A" ? amount : 0m;

            totalDebit += debit;
            totalCredit += credit;
            balance += debit - credit;

            rows.Add(new AccountingStatementRow(
                Convert.ToInt32(reader["ID"]),
                Convert.ToInt32(reader["Anno"]),
                Convert.ToInt32(reader["Settore"]),
                Convert.ToInt32(reader["Codice"]),
                DateOnly.FromDateTime(Convert.ToDateTime(reader["DataMov"])),
                Convert.ToString(reader["NumDoc"]) ?? "",
                ToNullableCode(reader["Causale"]),
                Convert.ToString(reader["DecCausale"]) ?? "",
                ToNullableCode(reader["CtPartita"]),
                Convert.ToString(reader["DecPartita"]) ?? "",
                debit,
                credit,
                balance,
                balance < 0 ? "A" : "D"));
        }
        await reader.DisposeAsync();

        return new AccountingStatementPageModel
        {
            StatementMode = "party",
            PageTitle = "Estratto conto clienti e fornitori",
            PageDescription = "Visualizzazione e stampa estratto conto clienti e fornitori",
            PartyType = partyType,
            PartyCode = partyCode,
            PartyName = party?.Name ?? "",
            Party = party,
            DateFrom = dateFrom,
            DateTo = dateTo,
            IsLoaded = true,
            Rows = rows,
            Parties = await ListPartiesAsync(connection, cancellationToken),
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            Balance = balance
        };
    }

    public async Task<AccountingStatementPageModel> GetAsync(
        int accountCode,
        DateOnly dateFrom,
        DateOnly dateTo,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var account = await FindAccountAsync(connection, accountCode, cancellationToken);
        var rows = new List<AccountingStatementRow>();

        var balance = await InitialBalanceAsync(connection, accountCode, dateFrom, cancellationToken);
        var totalDebit = 0m;
        var totalCredit = 0m;

        rows.Add(new AccountingStatementRow(
            null,
            null,
            null,
            null,
            null,
            "",
            null,
            "Saldo iniziale",
            null,
            "",
            balance >= 0 ? balance : 0,
            balance < 0 ? Math.Abs(balance) : 0,
            balance,
            balance < 0 ? "A" : "D"));

        const string sql = """
            SELECT rg.ID,
                   rg.Anno,
                   rg.Settore,
                   rg.Codice,
                   COALESCE(rg.Segno, '') AS Segno,
                   COALESCE(rg.Importo, 0) AS Importo,
                   m.DataMov,
                   COALESCE(m.NumDoc, '') AS NumDoc,
                   COALESCE(m.Causale, 0) AS Causale,
                   COALESCE(c.Descrizione, '') AS DecCausale,
                   COALESCE(m.CliFor, '') AS CliFor,
                   COALESCE(m.Ditta, 0) AS Ditta,
                   CASE
                       WHEN m.CliFor = 'C' THEN COALESCE(cli.Nome, '')
                       WHEN m.CliFor = 'F' THEN COALESCE(forn.Nome, '')
                       WHEN m.CliFor = 'D' THEN TRIM(CONCAT(COALESCE(dip.Cognome, ''), ' ', COALESCE(dip.Nome, '')))
                       ELSE COALESCE(ct.Descrizione, '')
                   END AS DecPartita
            FROM movcontrg rg
            LEFT JOIN movcont m ON m.ID = rg.ID
            LEFT JOIN causalicont c ON c.Codice = m.Causale
            LEFT JOIN clienti cli ON cli.Codice = m.Ditta
            LEFT JOIN fornitori forn ON forn.Codice = m.Ditta
            LEFT JOIN dipendenti dip ON dip.Codice = m.Ditta
            LEFT JOIN conti ct ON ct.Codice = m.Ditta
            WHERE rg.Conto = @accountCode
              AND m.DataMov BETWEEN @dateFrom AND @dateTo
            ORDER BY m.DataMov, m.Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@accountCode", MySqlDbType.Int32).Value = accountCode;
        command.Parameters.Add("@dateFrom", MySqlDbType.Date).Value = dateFrom.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@dateTo", MySqlDbType.Date).Value = dateTo.ToDateTime(TimeOnly.MinValue);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var sign = Convert.ToString(reader["Segno"]) ?? "";
            var amount = Money(reader["Importo"]);
            var debit = sign == "D" ? amount : 0m;
            var credit = sign == "A" ? amount : 0m;

            totalDebit += debit;
            totalCredit += credit;
            balance += debit - credit;

            rows.Add(new AccountingStatementRow(
                Convert.ToInt32(reader["ID"]),
                Convert.ToInt32(reader["Anno"]),
                Convert.ToInt32(reader["Settore"]),
                Convert.ToInt32(reader["Codice"]),
                DateOnly.FromDateTime(Convert.ToDateTime(reader["DataMov"])),
                Convert.ToString(reader["NumDoc"]) ?? "",
                ToNullableCode(reader["Causale"]),
                Convert.ToString(reader["DecCausale"]) ?? "",
                ToNullableCode(reader["Ditta"]),
                Convert.ToString(reader["DecPartita"]) ?? "",
                debit,
                credit,
                balance,
                balance < 0 ? "A" : "D"));
        }
        await reader.DisposeAsync();

        return new AccountingStatementPageModel
        {
            AccountCode = accountCode,
            AccountDescription = account?.AccountDescription ?? "",
            DateFrom = dateFrom,
            DateTo = dateTo,
            IsLoaded = true,
            Rows = rows,
            Accounts = await ListAccountsAsync(connection, cancellationToken),
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            Balance = balance
        };
    }

    public async Task<IReadOnlyList<AccountingStatementAccountLookupRow>> ListAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return await ListAccountsAsync(connection, cancellationToken);
    }

    private static async Task<IReadOnlyList<AccountingStatementAccountLookupRow>> ListAccountsAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(m.Codice, 0) AS Mastro,
                   COALESCE(m.Descrizione, '') AS DecMastro,
                   c.Codice AS Conto,
                   COALESCE(c.Descrizione, '') AS DecConto,
                   COALESCE(m.Tipo, c.Tipo, '') AS Tipo
            FROM conti c
            LEFT JOIN mastri m ON m.Codice = c.Mastro
            ORDER BY m.Codice, c.Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<AccountingStatementAccountLookupRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var type = Convert.ToString(reader["Tipo"]) ?? "";
            rows.Add(new AccountingStatementAccountLookupRow(
                Convert.ToInt32(reader["Mastro"]),
                Convert.ToString(reader["DecMastro"]) ?? "",
                Convert.ToInt32(reader["Conto"]),
                Convert.ToString(reader["DecConto"]) ?? "",
                type,
                TypeDescription(type)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<AccountingStatementPartyLookupRow>> ListPartiesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return await ListPartiesAsync(connection, cancellationToken);
    }

    private static async Task<IReadOnlyList<AccountingStatementPartyLookupRow>> ListPartiesAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 'C' AS Tipo,
                   Codice,
                   COALESCE(Nome, '') AS Nome,
                   COALESCE(Via, '') AS Via,
                   COALESCE(Citta, '') AS Citta,
                   COALESCE(Cap, '') AS Cap,
                   COALESCE(Prov, '') AS Prov,
                   COALESCE(Codfi, '') AS Codfi,
                   COALESCE(Piva, '') AS Piva
            FROM clienti
            UNION ALL
            SELECT 'F' AS Tipo,
                   Codice,
                   COALESCE(Nome, '') AS Nome,
                   COALESCE(Via, '') AS Via,
                   COALESCE(Citta, '') AS Citta,
                   COALESCE(Cap, '') AS Cap,
                   COALESCE(Prov, '') AS Prov,
                   COALESCE(Codfi, '') AS Codfi,
                   COALESCE(Piva, '') AS Piva
            FROM fornitori
            ORDER BY Tipo, Nome, Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<AccountingStatementPartyLookupRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new AccountingStatementPartyLookupRow(
                Convert.ToString(reader["Tipo"]) ?? "",
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Nome"]) ?? "",
                Convert.ToString(reader["Via"]) ?? "",
                Convert.ToString(reader["Citta"]) ?? "",
                Convert.ToString(reader["Cap"]) ?? "",
                Convert.ToString(reader["Prov"]) ?? "",
                Convert.ToString(reader["Codfi"]) ?? "",
                Convert.ToString(reader["Piva"]) ?? ""));
        }

        return rows;
    }

    private static async Task<AccountingStatementPartyLookupRow?> FindPartyAsync(
        MySqlConnection connection,
        string partyType,
        int partyCode,
        CancellationToken cancellationToken)
    {
        var parties = await ListPartiesAsync(connection, cancellationToken);
        return parties.FirstOrDefault(party =>
            party.Type == partyType &&
            party.Code == partyCode);
    }

    private static async Task<AccountingStatementAccountLookupRow?> FindAccountAsync(
        MySqlConnection connection,
        int accountCode,
        CancellationToken cancellationToken)
    {
        var accounts = await ListAccountsAsync(connection, cancellationToken);
        return accounts.FirstOrDefault(account => account.AccountCode == accountCode);
    }

    private static async Task<decimal> InitialBalanceAsync(
        MySqlConnection connection,
        int accountCode,
        DateOnly dateFrom,
        CancellationToken cancellationToken)
    {
        var year = dateFrom.Year;
        var dateStart = new DateOnly(year, 1, 1);
        var dateEnd = dateFrom.AddDays(-1);
        var balance = accountCode switch
        {
            81 => -await InitialCustomerSupplierMasterBalanceAsync(connection, year, "F", cancellationToken),
            82 => await InitialCustomerSupplierMasterBalanceAsync(connection, year, "C", cancellationToken),
            _ => 0m
        };

        if (dateEnd.Year < year)
        {
            return Math.Round(balance, 2);
        }

        balance += await AccountMovementBalanceAsync(
            connection,
            accountCode,
            dateStart,
            dateEnd,
            cancellationToken);
        return Math.Round(balance, 2);
    }

    private static async Task<decimal> InitialCustomerSupplierMasterBalanceAsync(
        MySqlConnection connection,
        int year,
        string type,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(SUM(Importo), 0)
            FROM SaldoIniCf
            WHERE Anno = @year AND CliFor = @type;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@year", MySqlDbType.Int32).Value = year;
        command.Parameters.Add("@type", MySqlDbType.VarChar).Value = type;
        return Money(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<decimal> PartyInitialBalanceAsync(
        MySqlConnection connection,
        int year,
        string type,
        int code,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(Importo, 0)
            FROM SaldoIniCf
            WHERE Anno = @year
              AND CliFor = @type
              AND Ditta = @code
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@year", MySqlDbType.Int32).Value = year;
        command.Parameters.Add("@type", MySqlDbType.VarChar).Value = type;
        command.Parameters.Add("@code", MySqlDbType.Int32).Value = code;
        var balance = Money(await command.ExecuteScalarAsync(cancellationToken));
        return type == "F" ? -balance : balance;
    }

    private static async Task<decimal> PartyMovementBalanceAsync(
        MySqlConnection connection,
        string type,
        int code,
        DateOnly dateStart,
        DateOnly dateEnd,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COALESCE(SUM(CASE WHEN c.Segno = 'D' THEN m.Importo ELSE 0 END), 0) AS Dare,
                COALESCE(SUM(CASE WHEN c.Segno = 'A' THEN m.Importo ELSE 0 END), 0) AS Avere
            FROM movcont m
            LEFT JOIN causalicont c ON c.Codice = m.Causale
            WHERE m.CliFor = @type
              AND m.Ditta = @code
              AND m.DataMov BETWEEN @dateStart AND @dateEnd;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@type", MySqlDbType.VarChar).Value = type;
        command.Parameters.Add("@code", MySqlDbType.Int32).Value = code;
        command.Parameters.Add("@dateStart", MySqlDbType.Date).Value = dateStart.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@dateEnd", MySqlDbType.Date).Value = dateEnd.ToDateTime(TimeOnly.MinValue);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return 0m;
        }

        return Money(reader["Dare"]) - Money(reader["Avere"]);
    }

    private static async Task<decimal> AccountMovementBalanceAsync(
        MySqlConnection connection,
        int accountCode,
        DateOnly dateStart,
        DateOnly dateEnd,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COALESCE(SUM(CASE WHEN rg.Segno = 'D' THEN rg.Importo ELSE 0 END), 0) AS Dare,
                COALESCE(SUM(CASE WHEN rg.Segno = 'A' THEN rg.Importo ELSE 0 END), 0) AS Avere
            FROM movcontrg rg
            LEFT JOIN movcont m ON m.ID = rg.ID
            WHERE rg.Conto = @accountCode
              AND m.DataMov BETWEEN @dateStart AND @dateEnd;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@accountCode", MySqlDbType.Int32).Value = accountCode;
        command.Parameters.Add("@dateStart", MySqlDbType.Date).Value = dateStart.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@dateEnd", MySqlDbType.Date).Value = dateEnd.ToDateTime(TimeOnly.MinValue);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return 0m;
        }

        return Money(reader["Dare"]) - Money(reader["Avere"]);
    }

    private static int? ToNullableCode(object value)
    {
        if (value is null or DBNull)
        {
            return null;
        }

        var number = Convert.ToInt32(value);
        return number == 0 ? null : number;
    }

    private static decimal Money(object? value) =>
        value is null or DBNull ? 0m : Math.Round(Convert.ToDecimal(value), 2);

    private static string NormalizePartyType(string value) =>
        string.Equals(value, "C", StringComparison.OrdinalIgnoreCase) ? "C" : "F";

    private static DateOnly DefaultDateTo(int year)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return today.Year == year ? today : new DateOnly(year, 12, 31);
    }

    private static string TypeDescription(string type) =>
        type switch
        {
            "P" => "Patrimoniale",
            "C" => "Costo",
            "R" => "Ricavo",
            _ => ""
        };
}
