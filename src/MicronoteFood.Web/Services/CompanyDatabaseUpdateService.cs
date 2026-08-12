using System.Text.RegularExpressions;
using System.Globalization;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Services;

public sealed class CompanyDatabaseUpdateService(
    MicronoteDb database,
    MasterRepository masterRepository)
{
    private const string FixedPrefix = "Fixed_";
    private const string SchemaPrefix = "Schema_";

    public static bool RequiresUpdate(CompanyMasterRecord company) =>
        company.RequiredDatabaseVersion is not null
        && (company.CurrentDatabaseVersion is null
            || company.CurrentDatabaseVersion < company.RequiredDatabaseVersion);

    public async Task<CompanyDatabaseUpdateResult> UpdateAsync(
        CompanyMasterRecord company,
        CancellationToken cancellationToken = default)
    {
        if (!RequiresUpdate(company) || company.RequiredDatabaseVersion is null)
        {
            return new(true, "Il database aziendale risulta gia' aggiornato.", null);
        }

        var masterDatabase = database.MasterDatabaseName;
        var companyDatabase = company.DatabaseName;
        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        var lockName = $"microfish-schema-update-{company.Code:0000}";
        if (!await AcquireLockAsync(connection, lockName, cancellationToken))
        {
            return new(false, "Aggiornamento gia' in corso per questa azienda.", null);
        }

        string? backupDatabase = null;
        try
        {
            backupDatabase = await BackupDatabaseAsync(
                connection,
                companyDatabase,
                cancellationToken);
            var templates = await ReadTemplatesAsync(
                connection,
                masterDatabase,
                cancellationToken);
            if (templates.Count == 0)
            {
                throw new InvalidOperationException("Lo stampo del database master e' vuoto.");
            }

            await ExecuteAsync(connection, "SET FOREIGN_KEY_CHECKS = 0;", cancellationToken);
            try
            {
                await EnsureCatalogAsync(
                    connection,
                    masterDatabase,
                    companyDatabase,
                    cancellationToken);
                foreach (var template in templates)
                {
                    await AlignTableAsync(
                        connection,
                        masterDatabase,
                        companyDatabase,
                        template,
                        cancellationToken);
                }

                foreach (var template in templates.Where(template => template.CopyData))
                {
                    if (!await TableDataMatchesAsync(
                            connection,
                            masterDatabase,
                            template.MasterTable,
                            companyDatabase,
                            template.TargetTable,
                            cancellationToken))
                    {
                        await ExecuteAsync(
                            connection,
                            $"DELETE FROM {Q(companyDatabase)}.{Q(template.TargetTable)}; " +
                            $"INSERT INTO {Q(companyDatabase)}.{Q(template.TargetTable)} " +
                            $"SELECT * FROM {Q(masterDatabase)}.{Q(template.MasterTable)};",
                            cancellationToken);
                    }
                }
            }
            finally
            {
                await ExecuteAsync(connection, "SET FOREIGN_KEY_CHECKS = 1;", cancellationToken);
            }

            await VerifyTemplateAsync(
                connection,
                masterDatabase,
                companyDatabase,
                templates,
                cancellationToken);
            await masterRepository.MarkCompanyDatabaseUpdatedAsync(
                company.Code,
                company.RequiredDatabaseVersion.Value,
                cancellationToken);
            return new(
                true,
                $"Database aggiornato alla versione {company.RequiredDatabaseVersion:yyyy-MM-dd HH:mm:ss}.",
                backupDatabase);
        }
        catch (Exception ex) when (ex is MySqlException or InvalidOperationException)
        {
            return new(
                false,
                "Aggiornamento del database non completato: " + ex.Message,
                backupDatabase);
        }
        finally
        {
            await ReleaseLockAsync(connection, lockName, cancellationToken);
        }
    }

    private static async Task<string> BackupDatabaseAsync(
        MySqlConnection connection,
        string sourceDatabase,
        CancellationToken cancellationToken)
    {
        var backupDatabase = $"{sourceDatabase}_Backup_{DateTime.Now:yyyyMMddHHmmss}";
        await ExecuteAsync(
            connection,
            $"CREATE DATABASE {Q(backupDatabase)} CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;",
            cancellationToken);
        var tables = await ReadTablesAsync(connection, sourceDatabase, cancellationToken);
        await ExecuteAsync(connection, "SET FOREIGN_KEY_CHECKS = 0;", cancellationToken);
        try
        {
            foreach (var table in tables)
            {
                await ExecuteAsync(
                    connection,
                    $"CREATE TABLE {Q(backupDatabase)}.{Q(table)} LIKE {Q(sourceDatabase)}.{Q(table)}; " +
                    $"INSERT INTO {Q(backupDatabase)}.{Q(table)} SELECT * FROM {Q(sourceDatabase)}.{Q(table)};",
                    cancellationToken);
            }
        }
        finally
        {
            await ExecuteAsync(connection, "SET FOREIGN_KEY_CHECKS = 1;", cancellationToken);
        }
        return backupDatabase;
    }

    private static async Task EnsureCatalogAsync(
        MySqlConnection connection,
        string masterDatabase,
        string companyDatabase,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, companyDatabase, "FixedTable", cancellationToken))
        {
            await ExecuteAsync(
                connection,
                $"CREATE TABLE {Q(companyDatabase)}.`FixedTable` LIKE {Q(masterDatabase)}.`FixedTable`;",
                cancellationToken);
        }
        if (!await TableDataMatchesAsync(
                connection,
                masterDatabase,
                "FixedTable",
                companyDatabase,
                "FixedTable",
                cancellationToken))
        {
            await ExecuteAsync(
                connection,
                $"DELETE FROM {Q(companyDatabase)}.`FixedTable`; " +
                $"INSERT INTO {Q(companyDatabase)}.`FixedTable` SELECT * FROM {Q(masterDatabase)}.`FixedTable`;",
                cancellationToken);
        }
    }

    private static async Task AlignTableAsync(
        MySqlConnection connection,
        string masterDatabase,
        string companyDatabase,
        TemplateTable template,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, companyDatabase, template.TargetTable, cancellationToken))
        {
            await ExecuteAsync(
                connection,
                $"CREATE TABLE {Q(companyDatabase)}.{Q(template.TargetTable)} " +
                $"LIKE {Q(masterDatabase)}.{Q(template.MasterTable)};",
                cancellationToken);
            return;
        }

        var definitions = await ReadColumnDefinitionsAsync(
            connection,
            masterDatabase,
            template.MasterTable,
            cancellationToken);
        var targetDefinitions = await ReadColumnDefinitionsAsync(
            connection,
            companyDatabase,
            template.TargetTable,
            cancellationToken);
        if (string.Equals(template.TargetTable, "VenditeRg", StringComparison.OrdinalIgnoreCase)
            && definitions.Any(definition => string.Equals(definition.Name, "AliqIva", StringComparison.OrdinalIgnoreCase))
            && targetDefinitions.Any(definition => string.Equals(definition.Name, "Iva", StringComparison.OrdinalIgnoreCase))
            && !targetDefinitions.Any(definition => string.Equals(definition.Name, "AliqIva", StringComparison.OrdinalIgnoreCase)))
        {
            var vatDefinition = definitions.First(definition => string.Equals(definition.Name, "AliqIva", StringComparison.OrdinalIgnoreCase));
            await ExecuteAsync(
                connection,
                $"ALTER TABLE {Q(companyDatabase)}.{Q(template.TargetTable)} " +
                $"CHANGE COLUMN `Iva` `AliqIva` {vatDefinition.Definition};",
                cancellationToken);
            targetDefinitions = await ReadColumnDefinitionsAsync(
                connection,
                companyDatabase,
                template.TargetTable,
                cancellationToken);
        }
        if (string.Equals(template.TargetTable, "MovCassa", StringComparison.OrdinalIgnoreCase)
            && definitions.Any(definition => string.Equals(
                definition.Name, "Annotazioni", StringComparison.OrdinalIgnoreCase))
            && targetDefinitions.Any(definition =>
                string.Equals(definition.Name, "Descrizione", StringComparison.OrdinalIgnoreCase)
                || string.Equals(definition.Name, "Ammotazioni", StringComparison.OrdinalIgnoreCase))
            && !targetDefinitions.Any(definition => string.Equals(
                definition.Name, "Annotazioni", StringComparison.OrdinalIgnoreCase)))
        {
            var notesDefinition = definitions.First(definition => string.Equals(
                definition.Name, "Annotazioni", StringComparison.OrdinalIgnoreCase));
            var oldNotesName = targetDefinitions.Any(definition => string.Equals(
                definition.Name, "Ammotazioni", StringComparison.OrdinalIgnoreCase))
                ? "Ammotazioni"
                : "Descrizione";
            await ExecuteAsync(
                connection,
                $"ALTER TABLE {Q(companyDatabase)}.{Q(template.TargetTable)} " +
                $"CHANGE COLUMN {Q(oldNotesName)} `Annotazioni` {notesDefinition.Definition};",
                cancellationToken);
            targetDefinitions = await ReadColumnDefinitionsAsync(
                connection,
                companyDatabase,
                template.TargetTable,
                cancellationToken);
        }
        if (template.CopyData && !DefinitionsMatch(definitions, targetDefinitions))
        {
            await ExecuteAsync(
                connection,
                $"DROP TABLE {Q(companyDatabase)}.{Q(template.TargetTable)}; " +
                $"CREATE TABLE {Q(companyDatabase)}.{Q(template.TargetTable)} " +
                $"LIKE {Q(masterDatabase)}.{Q(template.MasterTable)};",
                cancellationToken);
            return;
        }
        var targetByName = targetDefinitions.ToDictionary(
            definition => definition.Name,
            StringComparer.OrdinalIgnoreCase);
        string? previous = null;
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            var position = previous is null ? " FIRST" : $" AFTER {Q(previous)}";
            var exists = targetByName.TryGetValue(definition.Name, out var targetDefinition);
            var sameDefinition = exists
                && string.Equals(
                    NormalizeDefinition(definition.Definition),
                    NormalizeDefinition(targetDefinition!.Definition),
                    StringComparison.OrdinalIgnoreCase);
            var samePosition = index < targetDefinitions.Count
                && string.Equals(
                    targetDefinitions[index].Name,
                    definition.Name,
                    StringComparison.OrdinalIgnoreCase);
            if (sameDefinition && samePosition)
            {
                previous = definition.Name;
                continue;
            }

            var operation = exists ? "MODIFY COLUMN" : "ADD COLUMN";
            await ExecuteAsync(
                connection,
                $"ALTER TABLE {Q(companyDatabase)}.{Q(template.TargetTable)} " +
                $"{operation} {Q(definition.Name)} {definition.Definition}{position};",
                cancellationToken);
            previous = definition.Name;
        }
    }

    private static string NormalizeDefinition(string definition) =>
        Regex.Replace(definition.Trim(), "\\s+", " ");

    private static bool DefinitionsMatch(
        IReadOnlyList<ColumnDefinition> source,
        IReadOnlyList<ColumnDefinition> target) =>
        source.Count == target.Count
        && source.Zip(target).All(pair =>
            string.Equals(pair.First.Name, pair.Second.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                NormalizeDefinition(pair.First.Definition),
                NormalizeDefinition(pair.Second.Definition),
                StringComparison.OrdinalIgnoreCase));

    private static async Task<bool> TableDataMatchesAsync(
        MySqlConnection connection,
        string sourceDatabase,
        string sourceTable,
        string targetDatabase,
        string targetTable,
        CancellationToken cancellationToken)
    {
        var sourceRows = await ReadCanonicalRowsAsync(
            connection, sourceDatabase, sourceTable, cancellationToken);
        var targetRows = await ReadCanonicalRowsAsync(
            connection, targetDatabase, targetTable, cancellationToken);
        return sourceRows.SequenceEqual(targetRows, StringComparer.Ordinal);
    }

    private static async Task<IReadOnlyList<string>> ReadCanonicalRowsAsync(
        MySqlConnection connection,
        string databaseName,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            $"SELECT * FROM {Q(databaseName)}.{Q(tableName)};",
            connection);
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var values = new string[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
            {
                values[index] = CanonicalValue(reader.GetValue(index));
            }
            rows.Add(string.Join("|", values));
        }
        rows.Sort(StringComparer.Ordinal);
        return rows;
    }

    private static string CanonicalValue(object value)
    {
        if (value is DBNull)
        {
            return "N";
        }

        var text = value switch
        {
            byte[] bytes => Convert.ToBase64String(bytes),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };
        return $"V{text.Length}:{text}";
    }

    private static async Task VerifyTemplateAsync(
        MySqlConnection connection,
        string masterDatabase,
        string companyDatabase,
        IReadOnlyList<TemplateTable> templates,
        CancellationToken cancellationToken)
    {
        foreach (var template in templates)
        {
            var masterColumns = await ReadColumnsAsync(
                connection, masterDatabase, template.MasterTable, cancellationToken);
            var companyColumns = await ReadColumnsAsync(
                connection, companyDatabase, template.TargetTable, cancellationToken);
            var missing = masterColumns.Where(column => !companyColumns.Contains(column)).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Verifica non superata per {template.TargetTable}: " + string.Join(", ", missing));
            }
        }
    }

    private static async Task<IReadOnlyList<TemplateTable>> ReadTemplatesAsync(
        MySqlConnection connection,
        string masterDatabase,
        CancellationToken cancellationToken)
    {
        var tables = await ReadTablesAsync(connection, masterDatabase, cancellationToken);
        return tables
            .Where(table => table.StartsWith(FixedPrefix, StringComparison.OrdinalIgnoreCase)
                || table.StartsWith(SchemaPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(table => table.StartsWith(FixedPrefix, StringComparison.OrdinalIgnoreCase)
                ? new TemplateTable(table, table[FixedPrefix.Length..], true)
                : new TemplateTable(table, table[SchemaPrefix.Length..], false))
            .OrderBy(table => table.TargetTable, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<IReadOnlyList<ColumnDefinition>> ReadColumnDefinitionsAsync(
        MySqlConnection connection,
        string databaseName,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            $"SHOW CREATE TABLE {Q(databaseName)}.{Q(tableName)};",
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Definizione non trovata: {tableName}.");
        }
        var createSql = reader.GetString(1);
        var definitions = new List<ColumnDefinition>();
        foreach (var line in createSql.Split('\n'))
        {
            var match = Regex.Match(line, "^\\s*`(?<name>(?:``|[^`])+)`\\s+(?<definition>.+?)(?:,)?$", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                continue;
            }
            definitions.Add(new(
                match.Groups["name"].Value.Replace("``", "`"),
                match.Groups["definition"].Value.TrimEnd(',')));
        }
        return definitions;
    }

    private static async Task<HashSet<string>> ReadColumnsAsync(
        MySqlConnection connection,
        string databaseName,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT COLUMN_NAME
            FROM information_schema.COLUMNS
            WHERE LOWER(TABLE_SCHEMA) = LOWER(@database)
              AND LOWER(TABLE_NAME) = LOWER(@table)
            ORDER BY ORDINAL_POSITION;
            """,
            connection);
        command.Parameters.AddWithValue("@database", databaseName);
        command.Parameters.AddWithValue("@table", tableName);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    private static async Task<IReadOnlyList<string>> ReadTablesAsync(
        MySqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT TABLE_NAME
            FROM information_schema.TABLES
            WHERE LOWER(TABLE_SCHEMA) = LOWER(@database)
              AND TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_NAME;
            """,
            connection);
        command.Parameters.AddWithValue("@database", databaseName);
        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(reader.GetString(0));
        }
        return tables;
    }

    private static async Task<bool> TableExistsAsync(
        MySqlConnection connection,
        string databaseName,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT COUNT(*)
            FROM information_schema.TABLES
            WHERE LOWER(TABLE_SCHEMA) = LOWER(@database)
              AND LOWER(TABLE_NAME) = LOWER(@table)
              AND TABLE_TYPE = 'BASE TABLE';
            """,
            connection);
        command.Parameters.AddWithValue("@database", databaseName);
        command.Parameters.AddWithValue("@table", tableName);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private static async Task<bool> AcquireLockAsync(
        MySqlConnection connection,
        string name,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand("SELECT GET_LOCK(@name, 30);", connection);
        command.Parameters.AddWithValue("@name", name);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private static async Task ReleaseLockAsync(
        MySqlConnection connection,
        string name,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand("SELECT RELEASE_LOCK(@name);", connection);
        command.Parameters.AddWithValue("@name", name);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        MySqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Q(string identifier) => $"`{identifier.Replace("`", "``")}`";

    private sealed record TemplateTable(string MasterTable, string TargetTable, bool CopyData);
    private sealed record ColumnDefinition(string Name, string Definition);
}

public sealed record CompanyDatabaseUpdateResult(
    bool Success,
    string Message,
    string? BackupDatabase);
