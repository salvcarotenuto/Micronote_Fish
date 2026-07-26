using System.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class CustomerRepository(
    MicronoteDb database,
    ProgressiveCodeService progressiveCodes)
{
    public async Task<IReadOnlyList<CustomerListItem>> SearchAsync(
        string? search,
        int? accountCode,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                f.Codice,
                f.Nome,
                COALESCE(f.Citta, '') AS Citta,
                COALESCE(f.Prov, '') AS Prov,
                COALESCE(f.Piva, '') AS Piva,
                COALESCE(f.Telefono, '') AS Telefono,
                COALESCE(f.Email, '') AS Email,
                f.Contropartita,
                COALESCE(c.Descrizione, '') AS ContropartitaDescrizione,
                COALESCE(pv.Nome, '') AS PuntoVendita,
                COALESCE(f.Attivo, 1) AS Attivo
            FROM clienti f
            LEFT JOIN conti c ON c.Codice = f.Contropartita
            LEFT JOIN puntivendita pv ON pv.Codice = f.PuntoV
            WHERE
                (@search = ''
                 OR f.Nome LIKE CONCAT('%', @search, '%')
                 OR CAST(f.Codice AS CHAR) LIKE CONCAT('%', @search, '%'))
                AND (@accountCode IS NULL OR f.Contropartita = @accountCode)
            ORDER BY f.Nome, f.Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@search", search?.Trim() ?? "");
        command.Parameters.AddWithValue(
            "@accountCode",
            accountCode.HasValue ? accountCode.Value : DBNull.Value);

        var customers = new List<CustomerListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            customers.Add(new CustomerListItem(
                reader.GetInt32("Codice"),
                reader.GetString("Nome"),
                reader.GetString("Citta"),
                reader.GetString("Prov"),
                reader.GetString("Piva"),
                reader.GetString("Telefono"),
                reader.GetString("Email"),
                reader.IsDBNull("Contropartita")
                    ? null
                    : reader.GetInt32("Contropartita"),
                reader.GetString("ContropartitaDescrizione"),
                reader.GetString("PuntoVendita"),
                reader.GetBoolean("Attivo")));
        }

        return customers;
    }

    public async Task<CustomerEditModel?> GetAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT *
            FROM clienti
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

        return new CustomerEditModel
        {
            Code = reader.GetInt32("Codice"),
            Name = Text(reader, "Nome") ?? "",
            TaxCode = Text(reader, "Codfi"),
            VatNumber = Text(reader, "Piva"),
            City = Text(reader, "Citta"),
            PostalCode = Text(reader, "Cap"),
            Province = Text(reader, "Prov"),
            Address = Text(reader, "Via"),
            Phone = Text(reader, "Telefono"),
            Mobile = Text(reader, "Cellulare"),
            Email = Text(reader, "Email"),
            CertifiedEmail = Text(reader, "Pec"),
            Contact = Text(reader, "Contatto"),
            ContactMobile = Text(reader, "ContCell"),
            StoreCode = Integer(reader, "PuntoV"),
            IsActive = Boolean(reader, "Attivo", true),
            OpeningBalance = Decimal(reader, "SaldoIni"),
            CategoryCode = Integer(reader, "Categoria"),
            PriceList = Integer(reader, "Listino") ?? 0,
            CreditLimit = Decimal(reader, "Fido"),
            SdiCode = Text(reader, "CodSdi"),
            CountryCode = Integer(reader, "Nazione"),
            LegalNatureCode = Integer(reader, "Natura"),
            AccountCode = Integer(reader, "Contropartita"),
            AgentCode = Integer(reader, "Agente"),
            PaymentCode = Integer(reader, "Pagamento"),
            BankCode = Integer(reader, "Banca")
        };
    }

    public async Task<CustomerLookups> GetLookupsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        return new CustomerLookups(
            await LoadLookupAsync(
                connection,
                "SELECT Codice, Nome FROM puntivendita ORDER BY Nome",
                cancellationToken),
            await LoadLookupAsync(
                connection,
                "SELECT Codice, Descrizione FROM CategoriaCF ORDER BY Descrizione",
                cancellationToken),
            await LoadLookupAsync(
                connection,
                "SELECT Codice, Nome FROM nazioni WHERE Codice IS NOT NULL ORDER BY Nome",
                cancellationToken),
            await LoadLookupAsync(
                connection,
                "SELECT Codice, Descrizione FROM naturagiu WHERE Codice IS NOT NULL ORDER BY Descrizione",
                cancellationToken),
            await LoadLookupAsync(
                connection,
                "SELECT Codice, Descrizione FROM conti WHERE Tipo = 'R' ORDER BY Descrizione",
                cancellationToken),
            await LoadLookupAsync(
                connection,
                "SELECT Codice, Nome FROM agenti ORDER BY Nome",
                cancellationToken),
            await LoadLookupAsync(
                connection,
                "SELECT Codice, Descrizione FROM pagamenti ORDER BY Descrizione",
                cancellationToken),
            await LoadLookupAsync(
                connection,
                "SELECT Codice, Nome FROM banche ORDER BY Nome",
                cancellationToken));
    }

    public async Task<int> InsertAsync(
        CustomerEditModel customer,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        customer.Code = await ExecuteInsertAsync(
            connection,
            customer,
            cancellationToken);
        return customer.Code;
    }

    public Task<int> NextCodeAsync(CancellationToken cancellationToken = default) =>
        progressiveCodes.NextCodeAsync(
            "clienti",
            "Codice",
            cancellationToken: cancellationToken);

    public async Task<bool> UpdateAsync(
        CustomerEditModel customer,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        await using (var existsCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM clienti WHERE Codice = @code;",
            connection))
        {
            existsCommand.Parameters.AddWithValue("@code", customer.Code);
            if (Convert.ToInt32(
                await existsCommand.ExecuteScalarAsync(cancellationToken)) != 1)
            {
                return false;
            }
        }

        await ExecuteSaveAsync(
            connection,
            customer,
            cancellationToken);
        return true;
    }

    public async Task<CustomerDeleteResult> DeleteAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        var dependencies = new[]
        {
            ("movimenti", "movimenti di magazzino"),
            ("movcont", "movimenti contabili")
        };

        foreach (var (table, description) in dependencies)
        {
            var sql = $"""
                SELECT COUNT(*)
                FROM {table}
                WHERE CliFor = 'C' AND Ditta = @code;
                """;
            await using var dependencyCommand = new MySqlCommand(sql, connection);
            dependencyCommand.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(
                await dependencyCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return new CustomerDeleteResult(
                    false,
                    $"Il cliente non può essere eliminato: esistono {description}.");
            }
        }

        await using var command = new MySqlCommand(
            "DELETE FROM clienti WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);
        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

        return affectedRows == 1
            ? new CustomerDeleteResult(true, "Cliente eliminato.")
            : new CustomerDeleteResult(false, "Cliente non trovato.");
    }

    private static async Task ExecuteSaveAsync(
        MySqlConnection connection,
        CustomerEditModel customer,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE clienti
            SET
                Nome = @name,
                Codfi = @taxCode,
                Piva = @vatNumber,
                Citta = @city,
                Cap = @postalCode,
                Prov = @province,
                Via = @address,
                Telefono = @phone,
                Cellulare = @mobile,
                Email = @email,
                Pec = @certifiedEmail,
                Contatto = @contact,
                ContCell = @contactMobile,
                PuntoV = @storeCode,
                Attivo = @isActive,
                Categoria = @categoryCode,
                Listino = @priceList,
                Fido = @creditLimit,
                CodSdi = @sdiCode,
                Nazione = @countryCode,
                Natura = @legalNatureCode,
                Contropartita = @accountCode,
                Agente = @agentCode,
                Pagamento = @paymentCode,
                Banca = @bankCode
            WHERE Codice = @code;
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, customer);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<int> ExecuteInsertAsync(
        MySqlConnection connection,
        CustomerEditModel customer,
        CancellationToken cancellationToken)
    {
        customer.Code = await progressiveCodes.NextCodeAsync(
            connection,
            "clienti",
            "Codice",
            cancellationToken: cancellationToken);

        const string sql = """
            INSERT INTO clienti
            (
                Codice, Nome, Codfi, Piva, Citta, Cap, Prov, Via,
                Telefono, Cellulare, Email, Pec, Contatto, ContCell,
                PuntoV, Attivo, Categoria, Listino, Fido, CodSdi, Nazione, Natura,
                Contropartita, Agente, Pagamento, Banca
            )
            VALUES
            (
                @code, @name, @taxCode, @vatNumber, @city, @postalCode,
                @province, @address, @phone, @mobile, @email,
                @certifiedEmail, @contact, @contactMobile, @storeCode,
                @isActive, @categoryCode, @priceList, @creditLimit,
                @sdiCode, @countryCode, @legalNatureCode,
                @accountCode, @agentCode, @paymentCode, @bankCode
            );
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, customer);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return customer.Code;
    }

    private static void AddSaveParameters(
        MySqlCommand command,
        CustomerEditModel customer,
        bool includeCode = true)
    {
        if (includeCode)
        {
            command.Parameters.AddWithValue("@code", customer.Code);
        }

        command.Parameters.AddWithValue("@name", Clean(customer.Name));
        command.Parameters.AddWithValue("@taxCode", DbText(customer.TaxCode));
        command.Parameters.AddWithValue("@vatNumber", DbText(customer.VatNumber));
        command.Parameters.AddWithValue("@city", DbText(customer.City));
        command.Parameters.AddWithValue("@postalCode", DbText(customer.PostalCode));
        command.Parameters.AddWithValue("@province", DbText(customer.Province));
        command.Parameters.AddWithValue("@address", DbText(customer.Address));
        command.Parameters.AddWithValue("@phone", DbText(customer.Phone));
        command.Parameters.AddWithValue("@mobile", DbText(customer.Mobile));
        command.Parameters.AddWithValue("@email", DbText(customer.Email));
        command.Parameters.AddWithValue(
            "@certifiedEmail",
            DbText(customer.CertifiedEmail));
        command.Parameters.AddWithValue("@contact", DbText(customer.Contact));
        command.Parameters.AddWithValue(
            "@contactMobile",
            DbText(customer.ContactMobile));
        command.Parameters.AddWithValue("@storeCode", DbNumber(customer.StoreCode));
        command.Parameters.AddWithValue("@isActive", customer.IsActive ? 1 : 0);
        command.Parameters.AddWithValue(
            "@categoryCode",
            DbNumber(customer.CategoryCode));
        command.Parameters.AddWithValue("@priceList", customer.PriceList);
        command.Parameters.AddWithValue(
            "@creditLimit",
            customer.CreditLimit ?? 0m);
        command.Parameters.AddWithValue("@sdiCode", DbText(customer.SdiCode));
        command.Parameters.AddWithValue(
            "@countryCode",
            DbNumber(customer.CountryCode));
        command.Parameters.AddWithValue(
            "@legalNatureCode",
            DbNumber(customer.LegalNatureCode));
        command.Parameters.AddWithValue(
            "@accountCode",
            DbNumber(customer.AccountCode));
        command.Parameters.AddWithValue("@agentCode", DbNumber(customer.AgentCode));
        command.Parameters.AddWithValue(
            "@paymentCode",
            DbNumber(customer.PaymentCode));
        command.Parameters.AddWithValue("@bankCode", DbNumber(customer.BankCode));
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

    private static string? Text(MySqlDataReader reader, string name) =>
        reader.IsDBNull(name) ? null : reader.GetString(name);

    private static int? Integer(MySqlDataReader reader, string name) =>
        reader.IsDBNull(name) ? null : Convert.ToInt32(reader[name]);

    private static decimal? Decimal(MySqlDataReader reader, string name) =>
        reader.IsDBNull(name) ? null : Convert.ToDecimal(reader[name]);

    private static bool Boolean(
        MySqlDataReader reader,
        string name,
        bool defaultValue) =>
        reader.IsDBNull(name) ? defaultValue : Convert.ToBoolean(reader[name]);

    private static string Clean(string value) => value.Trim();

    private static object DbText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static object DbNumber(int? value) =>
        value.HasValue && value.Value > 0 ? value.Value : DBNull.Value;
}
