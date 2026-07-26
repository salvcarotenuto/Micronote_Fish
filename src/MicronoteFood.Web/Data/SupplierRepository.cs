using System.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class SupplierRepository(
    MicronoteDb database,
    ProgressiveCodeService progressiveCodes)
{
    public async Task<IReadOnlyList<SupplierListItem>> SearchAsync(
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
            FROM fornitori f
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

        var suppliers = new List<SupplierListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            suppliers.Add(new SupplierListItem(
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

        return suppliers;
    }

    public async Task<SupplierEditModel?> GetAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT *
            FROM fornitori
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

        return new SupplierEditModel
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

    public async Task<SupplierLookups> GetLookupsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        return new SupplierLookups(
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
                "SELECT Codice, Descrizione FROM conti WHERE Tipo = 'C' ORDER BY Descrizione",
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
        SupplierEditModel supplier,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        supplier.Code = await ExecuteInsertAsync(
            connection,
            supplier,
            cancellationToken);
        return supplier.Code;
    }

    public Task<int> NextCodeAsync(CancellationToken cancellationToken = default) =>
        progressiveCodes.NextCodeAsync(
            "fornitori",
            "Codice",
            cancellationToken: cancellationToken);

    public async Task<bool> UpdateAsync(
        SupplierEditModel supplier,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        await using (var existsCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM fornitori WHERE Codice = @code;",
            connection))
        {
            existsCommand.Parameters.AddWithValue("@code", supplier.Code);
            if (Convert.ToInt32(
                await existsCommand.ExecuteScalarAsync(cancellationToken)) != 1)
            {
                return false;
            }
        }

        await ExecuteSaveAsync(
            connection,
            supplier,
            cancellationToken);
        return true;
    }

    public async Task<SupplierDeleteResult> DeleteAsync(
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
                WHERE CliFor = 'F' AND Ditta = @code;
                """;
            await using var dependencyCommand = new MySqlCommand(sql, connection);
            dependencyCommand.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(
                await dependencyCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return new SupplierDeleteResult(
                    false,
                    $"Il fornitore non può essere eliminato: esistono {description}.");
            }
        }

        await using var command = new MySqlCommand(
            "DELETE FROM fornitori WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);
        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

        return affectedRows == 1
            ? new SupplierDeleteResult(true, "Fornitore eliminato.")
            : new SupplierDeleteResult(false, "Fornitore non trovato.");
    }

    private static async Task ExecuteSaveAsync(
        MySqlConnection connection,
        SupplierEditModel supplier,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE fornitori
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
                SaldoIni = @openingBalance,
                Categoria = @categoryCode,
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
        AddSaveParameters(command, supplier);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<int> ExecuteInsertAsync(
        MySqlConnection connection,
        SupplierEditModel supplier,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO fornitori
            (
                Nome, Codfi, Piva, Citta, Cap, Prov, Via,
                Telefono, Cellulare, Email, Pec, Contatto, ContCell,
                PuntoV, Attivo, SaldoIni, Categoria, Fido, CodSdi, Nazione, Natura,
                Contropartita, Agente, Pagamento, Banca
            )
            VALUES
            (
                @name, @taxCode, @vatNumber, @city, @postalCode,
                @province, @address, @phone, @mobile, @email,
                @certifiedEmail, @contact, @contactMobile, @storeCode,
                @isActive, @openingBalance, @categoryCode, @creditLimit,
                @sdiCode, @countryCode, @legalNatureCode,
                @accountCode, @agentCode, @paymentCode, @bankCode
            );
            """;

        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, supplier, includeCode: false);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return checked((int)command.LastInsertedId);
    }

    private static void AddSaveParameters(
        MySqlCommand command,
        SupplierEditModel supplier,
        bool includeCode = true)
    {
        if (includeCode)
        {
            command.Parameters.AddWithValue("@code", supplier.Code);
        }

        command.Parameters.AddWithValue("@name", Clean(supplier.Name));
        command.Parameters.AddWithValue("@taxCode", DbText(supplier.TaxCode));
        command.Parameters.AddWithValue("@vatNumber", DbText(supplier.VatNumber));
        command.Parameters.AddWithValue("@city", DbText(supplier.City));
        command.Parameters.AddWithValue("@postalCode", DbText(supplier.PostalCode));
        command.Parameters.AddWithValue("@province", DbText(supplier.Province));
        command.Parameters.AddWithValue("@address", DbText(supplier.Address));
        command.Parameters.AddWithValue("@phone", DbText(supplier.Phone));
        command.Parameters.AddWithValue("@mobile", DbText(supplier.Mobile));
        command.Parameters.AddWithValue("@email", DbText(supplier.Email));
        command.Parameters.AddWithValue(
            "@certifiedEmail",
            DbText(supplier.CertifiedEmail));
        command.Parameters.AddWithValue("@contact", DbText(supplier.Contact));
        command.Parameters.AddWithValue(
            "@contactMobile",
            DbText(supplier.ContactMobile));
        command.Parameters.AddWithValue("@storeCode", DbNumber(supplier.StoreCode));
        command.Parameters.AddWithValue("@isActive", supplier.IsActive ? 1 : 0);
        command.Parameters.AddWithValue(
            "@openingBalance",
            supplier.OpeningBalance.HasValue
                ? supplier.OpeningBalance.Value
                : DBNull.Value);
        command.Parameters.AddWithValue(
            "@categoryCode",
            DbNumber(supplier.CategoryCode));
        command.Parameters.AddWithValue(
            "@creditLimit",
            supplier.CreditLimit.HasValue
                ? supplier.CreditLimit.Value
                : DBNull.Value);
        command.Parameters.AddWithValue("@sdiCode", DbText(supplier.SdiCode));
        command.Parameters.AddWithValue(
            "@countryCode",
            DbNumber(supplier.CountryCode));
        command.Parameters.AddWithValue(
            "@legalNatureCode",
            DbNumber(supplier.LegalNatureCode));
        command.Parameters.AddWithValue(
            "@accountCode",
            DbNumber(supplier.AccountCode));
        command.Parameters.AddWithValue("@agentCode", DbNumber(supplier.AgentCode));
        command.Parameters.AddWithValue(
            "@paymentCode",
            DbNumber(supplier.PaymentCode));
        command.Parameters.AddWithValue("@bankCode", DbNumber(supplier.BankCode));
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
