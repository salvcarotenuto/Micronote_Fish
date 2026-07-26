using System.Text.RegularExpressions;
using MicronoteFood.Web.Data;
using MySqlConnector;

namespace MicronoteFood.Web.Services;

public sealed partial class ProgressiveCodeService(MicronoteDb database)
{
    public async Task<int> NextCodeAsync(
        string tableName,
        string codeColumn,
        IReadOnlyDictionary<string, object?>? filters = null,
        int minimumValue = 1,
        int? maxValue = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return await NextCodeAsync(
            connection,
            tableName,
            codeColumn,
            filters,
            minimumValue,
            maxValue,
            null,
            cancellationToken);
    }

    public async Task<int> NextCodeAsync(
        MySqlConnection connection,
        string tableName,
        string codeColumn,
        IReadOnlyDictionary<string, object?>? filters = null,
        int minimumValue = 1,
        int? maxValue = null,
        MySqlTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        if (minimumValue < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumValue), "Il codice minimo deve essere positivo.");
        }

        if (maxValue is not null && maxValue.Value < minimumValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maxValue), "Il codice massimo deve essere maggiore o uguale al minimo.");
        }

        var table = SqlIdentifier(tableName);
        var column = SqlIdentifier(codeColumn);
        var sql = $"""
            SELECT DISTINCT {column} AS Code
            FROM {table}
            WHERE {column} >= @minimumValue
            """;

        if (maxValue is not null)
        {
            sql += $"{Environment.NewLine}  AND {column} <= @maxValue";
        }

        var filterIndex = 0;
        foreach (var filter in filters ?? EmptyFilters())
        {
            var filterColumn = SqlIdentifier(filter.Key);
            sql += filter.Value is null
                ? $"{Environment.NewLine}  AND {filterColumn} IS NULL"
                : $"{Environment.NewLine}  AND {filterColumn} = @filter{filterIndex}";
            filterIndex += 1;
        }

        sql += $"{Environment.NewLine}ORDER BY {column};";

        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@minimumValue", minimumValue);
        if (maxValue is not null)
        {
            command.Parameters.AddWithValue("@maxValue", maxValue.Value);
        }

        filterIndex = 0;
        foreach (var filter in filters ?? EmptyFilters())
        {
            if (filter.Value is not null)
            {
                command.Parameters.AddWithValue($"@filter{filterIndex}", filter.Value);
            }

            filterIndex += 1;
        }

        var nextCode = minimumValue;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var currentCode = Convert.ToInt32(reader["Code"]);
            if (currentCode < nextCode)
            {
                continue;
            }

            if (currentCode > nextCode)
            {
                break;
            }

            nextCode += 1;
        }

        if (maxValue is not null && nextCode > maxValue.Value)
        {
            throw new InvalidOperationException(
                $"Non ci sono codici liberi in {tableName}.{codeColumn} tra {minimumValue} e {maxValue.Value}.");
        }

        return nextCode;
    }

    private static IReadOnlyDictionary<string, object?> EmptyFilters() =>
        new Dictionary<string, object?>();

    private static string SqlIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !IdentifierRegex().IsMatch(value))
        {
            throw new ArgumentException($"Identificatore SQL non valido: {value}", nameof(value));
        }

        return $"`{value}`";
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierRegex();
}
