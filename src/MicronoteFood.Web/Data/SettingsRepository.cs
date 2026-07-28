using System.Reflection;
using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class SettingsRepository(MicronoteDb database)
{
    private const string DefaultSalesVatRate = "10";
    private const string DefaultCounterSaleGrouping = "category";

    private static readonly PropertyInfo[] SettingsProperties =
        typeof(SettingsEditModel).GetProperties(BindingFlags.Instance | BindingFlags.Public);

    private static readonly HashSet<string> ReadOnlyOptionKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(SettingsEditModel.DataInventario)
        };

    public async Task<SettingsEditModel> GetAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        await EnsureDefaultOptionsAsync(cancellationToken);
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT Chiave, COALESCE(Valore, '') AS Valore
            FROM Opzioni;
            """,
            connection);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values[reader.GetString("Chiave")] = reader.GetString("Valore");
        }

        var settings = new SettingsEditModel();
        foreach (var property in SettingsProperties)
        {
            if (values.TryGetValue(property.Name, out var value))
            {
                property.SetValue(settings, ConvertFromStorage(property, value));
            }
        }

        if (string.IsNullOrWhiteSpace(settings.CartellaFeAcquisti))
        {
            settings.CartellaFeAcquisti = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FEAcquisti");
        }

        return settings;
    }

    public async Task SaveAsync(SettingsEditModel settings, CancellationToken cancellationToken = default)
    {
        NormalizeDefaults(settings);
        await EnsureTableAsync(cancellationToken);
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var property in SettingsProperties)
            {
                if (ReadOnlyOptionKeys.Contains(property.Name))
                {
                    continue;
                }

                var value = ConvertToStorage(property.GetValue(settings));
                await SaveOptionAsync(connection, transaction, property.Name, value, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void NormalizeDefaults(SettingsEditModel settings)
    {
        if (string.IsNullOrWhiteSpace(settings.AliqIvaVendite))
        {
            settings.AliqIvaVendite = DefaultSalesVatRate;
        }

        if (settings.RaggruppamentoVenditaBanco is not ("category" or "group" or "species" or "origin"))
        {
            settings.RaggruppamentoVenditaBanco = DefaultCounterSaleGrouping;
        }
    }

    private async Task EnsureDefaultOptionsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            INSERT INTO Opzioni (Chiave, Valore)
            SELECT 'AliqIvaVendite', @defaultValue
            WHERE NOT EXISTS (
                SELECT 1
                FROM Opzioni
                WHERE Chiave = 'AliqIvaVendite'
            );

            UPDATE Opzioni
            SET Valore = @defaultValue
            WHERE Chiave = 'AliqIvaVendite'
              AND (Valore IS NULL OR TRIM(Valore) = '');

            INSERT INTO Opzioni (Chiave, Valore)
            SELECT 'RaggruppamentoVenditaBanco', @defaultGrouping
            WHERE NOT EXISTS (
                SELECT 1
                FROM Opzioni
                WHERE Chiave = 'RaggruppamentoVenditaBanco'
            );

            UPDATE Opzioni
            SET Valore = @defaultGrouping
            WHERE Chiave = 'RaggruppamentoVenditaBanco'
              AND (Valore IS NULL
                   OR Valore NOT IN ('category', 'group', 'species', 'origin'));
            """,
            connection);
        command.Parameters.AddWithValue("@defaultValue", DefaultSalesVatRate);
        command.Parameters.AddWithValue("@defaultGrouping", DefaultCounterSaleGrouping);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            CREATE TABLE IF NOT EXISTS Opzioni (
                Chiave VARCHAR(100) NOT NULL PRIMARY KEY,
                Valore TEXT NULL
            );

            ALTER TABLE Opzioni
                MODIFY Chiave VARCHAR(100) NOT NULL,
                MODIFY Valore TEXT NULL;
            """,
            connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SaveOptionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        await using var update = new MySqlCommand(
            """
            UPDATE Opzioni
            SET Valore = @value
            WHERE Chiave = @key;
            """,
            connection,
            transaction);
        update.Parameters.AddWithValue("@key", key);
        update.Parameters.AddWithValue("@value", value);

        var changed = await update.ExecuteNonQueryAsync(cancellationToken);
        if (changed > 0)
        {
            return;
        }

        await using var insert = new MySqlCommand(
            """
            INSERT INTO Opzioni (Chiave, Valore)
            VALUES (@key, @value);
            """,
            connection,
            transaction);
        insert.Parameters.AddWithValue("@key", key);
        insert.Parameters.AddWithValue("@value", value);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    private static object ConvertFromStorage(PropertyInfo property, string value)
    {
        if (property.PropertyType == typeof(bool))
        {
            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("s", StringComparison.OrdinalIgnoreCase)
                || value.Equals("si", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        return value;
    }

    private static string ConvertToStorage(object? value)
    {
        if (value is bool flag)
        {
            return flag ? "1" : "0";
        }

        return Convert.ToString(value)?.Trim() ?? "";
    }
}

