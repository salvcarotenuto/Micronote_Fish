using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class PaymentCodeRepository(
    MicronoteDb database,
    ProgressiveCodeService progressiveCodes)
{
    public static IReadOnlyList<PaymentCodeNumberOption> StartFromOptions { get; } =
    [
        new(1, "Data fattura"),
        new(2, "Data sped."),
        new(3, "Fine mese"),
        new(4, "Data specifica")
    ];

    public async Task<IReadOnlyList<PaymentCodeListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                PG.Codice,
                COALESCE(PG.Descrizione, '') AS Descrizione,
                COALESCE(PG.Sigla, '') AS Sigla,
                COALESCE(TP.Descrizione, '') AS TipoPagamentoDescrizione,
                COALESCE(PG.NumScadenze, 0) AS NumScadenze,
                CASE
                    WHEN COALESCE(PG.Decorrenza, 0) = 1 THEN 'Data fattura'
                    WHEN COALESCE(PG.Decorrenza, 0) = 2 THEN 'Data sped.'
                    WHEN COALESCE(PG.Decorrenza, 0) = 3 THEN 'Fine mese'
                    WHEN COALESCE(PG.Decorrenza, 0) = 4 THEN 'Data specifica'
                    ELSE ''
                END AS DecorrenzaDescrizione
            FROM pagamenti AS PG
            LEFT JOIN tipopagamenti AS TP ON TP.Codice = PG.TipoPagamento
            ORDER BY PG.Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var payments = new List<PaymentCodeListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            payments.Add(new PaymentCodeListItem(
                Convert.ToInt32(reader["Codice"]),
                reader.GetString("Descrizione"),
                reader.GetString("Sigla"),
                reader.GetString("TipoPagamentoDescrizione"),
                Convert.ToInt32(reader["NumScadenze"]),
                reader.GetString("DecorrenzaDescrizione")));
        }

        return payments;
    }

    public async Task<PaymentCodeEditModel?> GetAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Descrizione, '') AS Descrizione,
                COALESCE(TipoPagamento, '') AS TipoPagamento,
                TipoTitolo,
                COALESCE(NumScadenze, 0) AS NumScadenze,
                COALESCE(PrimoInterv, 0) AS PrimoInterv,
                COALESCE(Intervallo, 0) AS Intervallo,
                COALESCE(TimeOffset, 0) AS TimeOffset,
                COALESCE(Condizioni, '') AS Condizioni,
                COALESCE(Modalita, '') AS Modalita,
                COALESCE(Sigla, '') AS Sigla,
                COALESCE(Spese, 0) AS Spese,
                COALESCE(Decorrenza, 0) AS Decorrenza,
                COALESCE(SkipAgo, 0) AS SkipAgo,
                COALESCE(SkipDic, 0) AS SkipDic
            FROM pagamenti
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

        return new PaymentCodeEditModel
        {
            IsNew = false,
            Code = Convert.ToInt32(reader["Codice"]),
            Description = reader.GetString("Descrizione"),
            PaymentType = reader.GetString("TipoPagamento"),
            TitleType = reader["TipoTitolo"] == DBNull.Value
                ? null
                : Convert.ToInt32(reader["TipoTitolo"]),
            DueDates = Convert.ToInt32(reader["NumScadenze"]),
            FirstInterval = Convert.ToInt32(reader["PrimoInterv"]),
            Interval = Convert.ToInt32(reader["Intervallo"]),
            TimeOffset = Convert.ToInt32(reader["TimeOffset"]),
            Conditions = reader.GetString("Condizioni"),
            Mode = reader.GetString("Modalita"),
            Abbreviation = reader.GetString("Sigla"),
            Expenses = Convert.ToDecimal(reader["Spese"]),
            StartFrom = Convert.ToInt32(reader["Decorrenza"]),
            SkipAugust = Convert.ToInt32(reader["SkipAgo"]) != 0,
            SkipDecember = Convert.ToInt32(reader["SkipDic"]) != 0
        };
    }

    public async Task<IReadOnlyList<PaymentCodeOption>> ListPaymentTypeOptionsAsync(
        CancellationToken cancellationToken = default) =>
        await ListTextOptionsAsync("tipopagamenti", cancellationToken);

    public async Task<IReadOnlyList<PaymentCodeNumberOption>> ListTitleTypeOptionsAsync(
        CancellationToken cancellationToken = default) =>
        await ListNumberOptionsAsync("tipotitoli", cancellationToken);

    public async Task<IReadOnlyList<PaymentCodeOption>> ListConditionOptionsAsync(
        CancellationToken cancellationToken = default) =>
        await ListTextOptionsAsync("FeCondPag", cancellationToken);

    public async Task<IReadOnlyList<PaymentCodeOption>> ListModeOptionsAsync(
        CancellationToken cancellationToken = default) =>
        await ListTextOptionsAsync("FeModoPag", cancellationToken);

    public Task<int> NextCodeAsync(CancellationToken cancellationToken = default) =>
        progressiveCodes.NextCodeAsync(
            "pagamenti",
            "Codice",
            cancellationToken: cancellationToken);

    public async Task<bool> ExistsAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM pagamenti WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task InsertAsync(
        PaymentCodeEditModel payment,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO pagamenti
                (Codice, Descrizione, TipoPagamento, TipoTitolo, NumScadenze,
                 PrimoInterv, Intervallo, TimeOffset, Condizioni, Modalita,
                 Sigla, Spese, Decorrenza, SkipAgo, SkipDic)
            VALUES
                (@code, @description, @paymentType, @titleType, @dueDates,
                 @firstInterval, @interval, @timeOffset, @conditions, @mode,
                 @abbreviation, @expenses, @startFrom, @skipAugust, @skipDecember);
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        payment.Code = await progressiveCodes.NextCodeAsync(
            connection,
            "pagamenti",
            "Codice",
            cancellationToken: cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, payment);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(
        PaymentCodeEditModel payment,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE pagamenti
            SET
                Descrizione = @description,
                TipoPagamento = @paymentType,
                TipoTitolo = @titleType,
                NumScadenze = @dueDates,
                PrimoInterv = @firstInterval,
                Intervallo = @interval,
                TimeOffset = @timeOffset,
                Condizioni = @conditions,
                Modalita = @mode,
                Sigla = @abbreviation,
                Spese = @expenses,
                Decorrenza = @startFrom,
                SkipAgo = @skipAugust,
                SkipDic = @skipDecember
            WHERE Codice = @code;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, payment);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<PaymentCodeDeleteResult> DeleteAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        await using (var linkedCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM moviva WHERE Pagamento = @code;",
            connection))
        {
            linkedCommand.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(await linkedCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return new PaymentCodeDeleteResult(
                    false,
                    "Il codice pagamento non può essere eliminato: esistono movimenti IVA collegati.");
            }
        }

        await using (var linkedCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM fatture WHERE Pagamento = @code;",
            connection))
        {
            linkedCommand.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(await linkedCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return new PaymentCodeDeleteResult(
                    false,
                    "Il codice pagamento non può essere eliminato: esistono fatture collegate.");
            }
        }

        await using var command = new MySqlCommand(
            "DELETE FROM pagamenti WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? new PaymentCodeDeleteResult(true, "Codice pagamento eliminato.")
            : new PaymentCodeDeleteResult(false, "Codice pagamento non trovato.");
    }

    private async Task<IReadOnlyList<PaymentCodeOption>> ListTextOptionsAsync(
        string table,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT
                Codice,
                COALESCE(Descrizione, '') AS Descrizione
            FROM {table}
            ORDER BY Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var options = new List<PaymentCodeOption>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                options.Add(new PaymentCodeOption(
                    Convert.ToString(reader["Codice"]) ?? "",
                    reader.GetString("Descrizione")));
            }
        }
        catch (MySqlException ex) when (ex.Number == 1146)
        {
            return [];
        }

        return options;
    }

    private async Task<IReadOnlyList<PaymentCodeNumberOption>> ListNumberOptionsAsync(
        string table,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT
                Codice,
                COALESCE(Descrizione, '') AS Descrizione
            FROM {table}
            ORDER BY Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var options = new List<PaymentCodeNumberOption>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                options.Add(new PaymentCodeNumberOption(
                    Convert.ToInt32(reader["Codice"]),
                    reader.GetString("Descrizione")));
            }
        }
        catch (MySqlException ex) when (ex.Number == 1146)
        {
            return [];
        }

        return options;
    }

    private static void AddSaveParameters(MySqlCommand command, PaymentCodeEditModel payment)
    {
        command.Parameters.AddWithValue("@code", payment.Code);
        command.Parameters.AddWithValue("@description", payment.Description.Trim());
        command.Parameters.AddWithValue("@paymentType", DbText(payment.PaymentType));
        command.Parameters.AddWithValue("@titleType", DbNumber(payment.TitleType));
        command.Parameters.AddWithValue("@dueDates", payment.DueDates);
        command.Parameters.AddWithValue("@firstInterval", payment.FirstInterval);
        command.Parameters.AddWithValue("@interval", payment.Interval);
        command.Parameters.AddWithValue("@timeOffset", payment.TimeOffset);
        command.Parameters.AddWithValue("@conditions", DbText(payment.Conditions));
        command.Parameters.AddWithValue("@mode", DbText(payment.Mode));
        command.Parameters.AddWithValue("@abbreviation", DbText(payment.Abbreviation));
        command.Parameters.AddWithValue("@expenses", payment.Expenses);
        command.Parameters.AddWithValue("@startFrom", payment.StartFrom);
        command.Parameters.AddWithValue("@skipAugust", payment.SkipAugust ? 1 : 0);
        command.Parameters.AddWithValue("@skipDecember", payment.SkipDecember ? 1 : 0);
    }

    private static object DbText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim().ToUpperInvariant();

    private static object DbNumber(int? value) =>
        value.HasValue && value.Value > 0 ? value.Value : DBNull.Value;
}
