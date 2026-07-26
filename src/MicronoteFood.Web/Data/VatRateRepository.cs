using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class VatRateRepository(MicronoteDb database)
{
    public async Task<IReadOnlyList<VatRateListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Descrizione, '') AS Descrizione,
                COALESCE(Aliquota, 0) AS Aliquota,
                COALESCE(Detrazione, 0) AS Detrazione,
                COALESCE(FeNatura, '') AS FeNatura
            FROM codiciiva
            ORDER BY Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var rates = new List<VatRateListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rates.Add(new VatRateListItem(
                Convert.ToString(reader["Codice"]) ?? "",
                reader.GetString("Descrizione"),
                Convert.ToDecimal(reader["Aliquota"]),
                Convert.ToDecimal(reader["Detrazione"]),
                reader.GetString("FeNatura")));
        }

        return rates;
    }

    public async Task<VatRateEditModel?> GetAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Descrizione, '') AS Descrizione,
                COALESCE(Aliquota, 0) AS Aliquota,
                COALESCE(Detrazione, 0) AS Detrazione,
                COALESCE(FeNatura, '') AS FeNatura
            FROM codiciiva
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

        return new VatRateEditModel
        {
            IsNew = false,
            Code = Convert.ToString(reader["Codice"]) ?? "",
            Description = reader.GetString("Descrizione"),
            Rate = Convert.ToDecimal(reader["Aliquota"]),
            Deduction = Convert.ToDecimal(reader["Detrazione"]),
            ElectronicInvoiceNature = reader.GetString("FeNatura")
        };
    }

    public async Task<IReadOnlyList<VatNatureOption>> ListNatureOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Codice,
                COALESCE(Descrizione, '') AS Descrizione
            FROM fecodiciiva
            ORDER BY Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var options = new List<VatNatureOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            options.Add(new VatNatureOption(
                Convert.ToString(reader["Codice"]) ?? "",
                reader.GetString("Descrizione")));
        }

        return options;
    }

    public async Task<bool> ExistsAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM codiciiva WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task InsertAsync(
        VatRateEditModel rate,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO codiciiva (Codice, Descrizione, Aliquota, Detrazione, FeNatura)
            VALUES (@code, @description, @rate, @deduction, @nature);
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, rate);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(
        VatRateEditModel rate,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE codiciiva
            SET
                Descrizione = @description,
                Aliquota = @rate,
                Detrazione = @deduction,
                FeNatura = @nature
            WHERE Codice = @code;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        AddSaveParameters(command, rate);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<VatRateDeleteResult> DeleteAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        await using (var linkedCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM articoli WHERE Codiva = @code;",
            connection))
        {
            linkedCommand.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(await linkedCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return new VatRateDeleteResult(
                    false,
                    "L'aliquota IVA non può essere eliminata: esistono articoli collegati.");
            }
        }

        await using (var linkedCommand = new MySqlCommand(
            """
            SELECT COUNT(*)
            FROM moviva
            WHERE Codiva1 = @code OR Codiva2 = @code OR Codiva3 = @code;
            """,
            connection))
        {
            linkedCommand.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(await linkedCommand.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return new VatRateDeleteResult(
                    false,
                    "L'aliquota IVA non può essere eliminata: esistono movimenti IVA collegati.");
            }
        }

        await using var command = new MySqlCommand(
            "DELETE FROM codiciiva WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? new VatRateDeleteResult(true, "Aliquota IVA eliminata.")
            : new VatRateDeleteResult(false, "Aliquota IVA non trovata.");
    }

    private static void AddSaveParameters(MySqlCommand command, VatRateEditModel rate)
    {
        command.Parameters.AddWithValue("@code", rate.Code.Trim());
        command.Parameters.AddWithValue("@description", rate.Description.Trim());
        command.Parameters.AddWithValue("@rate", rate.Rate);
        command.Parameters.AddWithValue("@deduction", rate.Deduction);
        command.Parameters.AddWithValue("@nature", DbText(rate.ElectronicInvoiceNature));
    }

    private static object DbText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim().ToUpperInvariant();
}
