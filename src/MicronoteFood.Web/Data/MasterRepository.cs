using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class MasterRepository(
    MicronoteDb database,
    MicronoteDatabaseOptions options)
{
    private const string FixedTableCatalog = "FixedTable";
    private const string FixedTemplatePrefix = "Fixed_";
    private const string SchemaTemplatePrefix = "Schema_";
    private const string DatabaseSchemaVersionKey = "VersioneSchemaDatabase";

    private static readonly string[] FixedTables =
    [
        "Aspetto",
        "CausaliCont",
        "CausaliCassa",
        "CausaliMag",
        "Codiciiva",
        "Comuni",
        "Conti",
        "Fecasseprev",
        "Fecausalepag",
        "Fecausrit",
        "Fecodiciiva",
        "Fecondpag",
        "Feformato",
        "Femodopag",
        "Feregimif",
        "Fetipodoc",
        "Fetiporit",
        "Mastri",
        "Naturagiu",
        "Nazioni",
        "Pagamenti",
        "Province",
        "Qualifiche",
        "Settori",
        "Tipopagamenti",
        "Tipotitoli",
        "Umisura"
    ];
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);

        // Installazioni precedenti usavano ParametriApp. La rinomina conserva i
        // valori esistenti e rende Parametri il nome canonico del master.
        if (await TableExistsAsync(connection, "ParametriApp", cancellationToken)
            && !await TableExistsAsync(connection, "Parametri", cancellationToken))
        {
            await ExecuteAsync(
                connection,
                "RENAME TABLE `ParametriApp` TO `Parametri`;",
                cancellationToken);
        }

        await using var command = new MySqlCommand(
            """
            CREATE TABLE IF NOT EXISTS Aziende (
                Codice INT NOT NULL PRIMARY KEY,
                Nome VARCHAR(120) NOT NULL,
                Password VARCHAR(255) NOT NULL,
                Attiva TINYINT(1) NOT NULL DEFAULT 1,
                Bloccata TINYINT(1) NOT NULL DEFAULT 0,
                NomeDatabase VARCHAR(120) NULL,
                VersioneDbAttuale DATETIME NULL,
                VersioneDbRichiesta DATETIME NULL,
                UNIQUE KEY UX_Aziende_Nome (Nome)
            );

            CREATE TABLE IF NOT EXISTS Parametri (
                Chiave VARCHAR(100) NOT NULL PRIMARY KEY,
                Valore TEXT NULL,
                VersioneSchemaDatabase DATETIME NULL
            );

            CREATE TABLE IF NOT EXISTS Accessi (
                Utente INT UNSIGNED NOT NULL,
                Azienda INT UNSIGNED NOT NULL,
                TheDate DATE NULL,
                TheTime TIME NULL
            );

            CREATE TABLE IF NOT EXISTS FixedTable (
                Nome VARCHAR(254) NOT NULL,
                Descrizione VARCHAR(254) NULL,
                Record INT NULL,
                UNIQUE KEY UX_FixedTable_Nome (Nome)
            );
            """,
            connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
        if (!await ColumnExistsAsync(
                connection,
                "Parametri",
                "VersioneSchemaDatabase",
                cancellationToken))
        {
            await ExecuteAsync(
                connection,
                "ALTER TABLE Parametri ADD COLUMN VersioneSchemaDatabase DATETIME NULL;",
                cancellationToken);
        }

        await using (var versionCommand = new MySqlCommand(
            """
            INSERT INTO Parametri (Chiave, VersioneSchemaDatabase)
            SELECT @key, @version
            WHERE NOT EXISTS (
                SELECT 1
                FROM Parametri
                WHERE Chiave = @key
            );
            """,
            connection))
        {
            versionCommand.Parameters.AddWithValue("@key", DatabaseSchemaVersionKey);
            versionCommand.Parameters.AddWithValue("@version", DateTime.Now);
            await versionCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var table in FixedTables.Append(FixedTableCatalog))
        {
            await using var fixedTableCommand = new MySqlCommand(
                """
                INSERT INTO FixedTable (Nome)
                SELECT @name
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM FixedTable
                    WHERE LOWER(Nome) = LOWER(@name)
                );
                """,
                connection);
            fixedTableCommand.Parameters.AddWithValue("@name", table);
            await fixedTableCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var normalizeCommand = new MySqlCommand(
            """
            UPDATE Aziende
            SET NomeDatabase = CONCAT('mn_', LPAD(Codice, 4, '0'))
            WHERE NomeDatabase IS NULL
               OR TRIM(NomeDatabase) = ''
               OR NomeDatabase LIKE 'Colf24%';
            """,
            connection);
        await normalizeCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyMasterRecord>> ListCompaniesAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT
                Codice,
                COALESCE(Nome, '') AS Nome,
                COALESCE(Password, '') AS Password,
                COALESCE(Attiva, 1) AS Attiva,
                COALESCE(Bloccata, 0) AS Bloccata,
                COALESCE(NomeDatabase, '') AS NomeDatabase,
                VersioneDbAttuale,
                VersioneDbRichiesta
            FROM Aziende
            ORDER BY Codice;
            """,
            connection);

        var companies = new List<CompanyMasterRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            companies.Add(ReadCompany(reader));
        }

        for (var index = 0; index < companies.Count; index++)
        {
            var company = companies[index];
            companies[index] = company with
            {
                DatabaseExists = !string.IsNullOrWhiteSpace(company.DatabaseName)
                    && await DatabaseExistsAsync(company.DatabaseName, cancellationToken)
            };
        }

        return companies;
    }

    public async Task<CompanyMasterRecord?> FindCompanyByNameAsync(
        string companyName,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT
                Codice,
                COALESCE(Nome, '') AS Nome,
                COALESCE(Password, '') AS Password,
                COALESCE(Attiva, 1) AS Attiva,
                COALESCE(Bloccata, 0) AS Bloccata,
                COALESCE(NomeDatabase, '') AS NomeDatabase,
                VersioneDbAttuale,
                VersioneDbRichiesta
            FROM Aziende
            WHERE LOWER(Nome) = LOWER(@name)
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("@name", companyName.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadCompany(reader)
            : null;
    }

    public async Task<CompanyMasterRecord?> FindCompanyByCodeAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT
                Codice,
                COALESCE(Nome, '') AS Nome,
                COALESCE(Password, '') AS Password,
                COALESCE(Attiva, 1) AS Attiva,
                COALESCE(Bloccata, 0) AS Bloccata,
                COALESCE(NomeDatabase, '') AS NomeDatabase,
                VersioneDbAttuale,
                VersioneDbRichiesta
            FROM Aziende
            WHERE Codice = @code
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("@code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadCompany(reader)
            : null;
    }

    public async Task MarkCompanyDatabaseUpdatedAsync(
        int code,
        DateTime version,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            UPDATE Aziende
            SET VersioneDbAttuale = @version
            WHERE Codice = @code;
            """,
            connection);
        command.Parameters.AddWithValue("@version", version);
        command.Parameters.AddWithValue("@code", code);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("Impossibile registrare la nuova versione del database aziendale.");
        }
    }

    public async Task<CompanyLoginResult> CheckCompanyLoginAsync(
        string companyName,
        string companyPassword,
        CancellationToken cancellationToken = default)
    {
        var company = await FindCompanyByNameAsync(companyName, cancellationToken);
        if (company is null)
        {
            return new CompanyLoginResult(
                CompanyLoginStatus.CompanyNotFound,
                null,
                "Azienda non trovata.");
        }

        if (!string.Equals(company.Password, companyPassword, StringComparison.OrdinalIgnoreCase))
        {
            return new CompanyLoginResult(
                CompanyLoginStatus.InvalidCompanyPassword,
                company,
                "Password azienda non valida.");
        }

        if (!company.Active)
        {
            return new CompanyLoginResult(
                CompanyLoginStatus.CompanyInactive,
                company,
                "Azienda non attiva.");
        }

        if (company.Locked)
        {
            return new CompanyLoginResult(
                CompanyLoginStatus.CompanyLocked,
                company,
                "Azienda bloccata.");
        }

        if (options.UseCompanyDatabase)
        {
            if (!await DatabaseExistsAsync(company.DatabaseName, cancellationToken))
            {
                return new CompanyLoginResult(
                    CompanyLoginStatus.CompanyDatabaseMissing,
                    company,
                    "Database aziendale non trovato.");
            }

            if (!await CompanyDatabaseIsReadyAsync(company.DatabaseName, cancellationToken))
            {
                return new CompanyLoginResult(
                    CompanyLoginStatus.CompanyDatabaseNotReady,
                    company,
                    "Database aziendale non pronto.");
            }
        }

        return new CompanyLoginResult(
            CompanyLoginStatus.Success,
            company,
            "Accesso azienda autorizzato.");
    }

    public async Task<string?> GetAppParameterAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT Valore FROM Parametri WHERE Chiave = @key LIMIT 1;",
            connection);
        command.Parameters.AddWithValue("@key", key.Trim());
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task SaveAppParameterAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            INSERT INTO Parametri (Chiave, Valore)
            VALUES (@key, @value)
            ON DUPLICATE KEY UPDATE Valore = VALUES(Valore);
            """,
            connection);
        command.Parameters.AddWithValue("@key", key.Trim());
        command.Parameters.AddWithValue("@value", value.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var normalizeCommand = new MySqlCommand(
            """
            UPDATE Aziende
            SET NomeDatabase = CONCAT('mn_', LPAD(Codice, 4, '0'))
            WHERE NomeDatabase IS NULL
               OR TRIM(NomeDatabase) = ''
               OR NomeDatabase LIKE 'Colf24%';
            """,
            connection);
        await normalizeCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> NextCompanyCodeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT Codice FROM Aziende ORDER BY Codice;",
            connection);

        var used = new HashSet<int>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            used.Add(Convert.ToInt32(reader["Codice"]));
        }

        for (var code = 1; code <= 9999; code++)
        {
            if (!used.Contains(code))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Non sono disponibili nuovi codici azienda.");
    }

    public async Task<CompanyMasterEditModel?> GetCompanyEditAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT
                Codice,
                COALESCE(Nome, '') AS Nome,
                COALESCE(Password, '') AS Password,
                COALESCE(Attiva, 1) AS Attiva,
                COALESCE(Bloccata, 0) AS Bloccata,
                COALESCE(NomeDatabase, '') AS NomeDatabase,
                VersioneDbAttuale,
                VersioneDbRichiesta
            FROM Aziende
            WHERE Codice = @code
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("@code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var databaseName = Text(reader, "NomeDatabase") ?? options.BuildCompanyDatabaseName(code);


        return new CompanyMasterEditModel
        {
            Code = code,
            Name = Convert.ToString(reader["Nome"]) ?? "",
            Password = Convert.ToString(reader["Password"]) ?? "",
            Active = Convert.ToBoolean(reader["Attiva"]),
            Locked = Convert.ToBoolean(reader["Bloccata"]),
            DatabaseName = databaseName,
            CurrentDatabaseVersion = DateTimeValue(reader, "VersioneDbAttuale"),
            RequiredDatabaseVersion = DateTimeValue(reader, "VersioneDbRichiesta"),
            IsNew = false
        };
    }

    public async Task<IReadOnlyDictionary<string, string>> ValidateCompanyAsync(
        CompanyMasterEditModel company,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (company.Code is < 1 or > 9999)
        {
            errors[nameof(company.Code)] = "Indicare un codice azienda valido.";
        }

        if (string.IsNullOrWhiteSpace(company.Name))
        {
            errors[nameof(company.Name)] = "Indicare il nome di accesso azienda.";
        }
        else if (await CompanyNameExistsAsync(
            company.Name,
            company.IsNew ? null : company.Code,
            cancellationToken))
        {
            errors[nameof(company.Name)] = "Nome di accesso azienda gia presente.";
        }

        if (string.IsNullOrWhiteSpace(company.Password))
        {
            errors[nameof(company.Password)] = "Indicare la password azienda.";
        }

        if (string.IsNullOrWhiteSpace(company.DatabaseName))
        {
            errors[nameof(company.DatabaseName)] = "Indicare il nome database.";
        }

        return errors;
    }

    public async Task InsertCompanyAsync(
        CompanyMasterEditModel company,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        company.DatabaseName = options.BuildCompanyDatabaseName(company.Code);


        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            INSERT INTO Aziende
            (
                Codice,
                Nome,
                Password,
                Attiva,
                Bloccata,
                NomeDatabase,
                VersioneDbAttuale,
                VersioneDbRichiesta
            )
            VALUES
            (
                @code,
                @name,
                @password,
                @active,
                @locked,
                @databaseName,
                @currentVersion,
                @requiredVersion
            );
            """,
            connection);
        AddCompanyParameters(command, company);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var normalizeCommand = new MySqlCommand(
            """
            UPDATE Aziende
            SET NomeDatabase = CONCAT('mn_', LPAD(Codice, 4, '0'))
            WHERE NomeDatabase IS NULL
               OR TRIM(NomeDatabase) = ''
               OR NomeDatabase LIKE 'Colf24%';
            """,
            connection);
        await normalizeCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> UpdateCompanyAsync(
        CompanyMasterEditModel company,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        company.DatabaseName = options.BuildCompanyDatabaseName(company.Code);


        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            UPDATE Aziende
            SET
                Nome = @name,
                Password = @password,
                Attiva = @active,
                Bloccata = @locked,
                NomeDatabase = @databaseName,
                VersioneDbAttuale = @currentVersion,
                VersioneDbRichiesta = @requiredVersion
            WHERE Codice = @code;
            """,
            connection);
        AddCompanyParameters(command, company);
        var changed = await command.ExecuteNonQueryAsync(cancellationToken);
        return changed > 0 || await CompanyCodeExistsAsync(company.Code, cancellationToken);
    }

    public async Task<CompanyDatabaseServiceResult> CreateCompanyDatabaseAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var company = await GetCompanyEditAsync(code, cancellationToken);
        if (company is null)
        {
            return new CompanyDatabaseServiceResult(false, "Azienda non trovata.");
        }

        var sourceDatabase = options.MasterDatabase;
        var targetDatabase = string.IsNullOrWhiteSpace(company.DatabaseName)
            ? options.BuildCompanyDatabaseName(code)
            : company.DatabaseName.Trim();
        if (await DatabaseExistsAsync(targetDatabase, cancellationToken))
        {
            return new CompanyDatabaseServiceResult(false, $"Il database {targetDatabase} esiste già.");
        }

        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        await ExecuteAsync(connection, $"CREATE DATABASE `{targetDatabase}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;", cancellationToken);

        var tables = await ListTablesAsync(connection, sourceDatabase, cancellationToken);
        var fixedTemplates = tables
            .Where(table => table.StartsWith(FixedTemplatePrefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                table => table[FixedTemplatePrefix.Length..],
                table => table,
                StringComparer.OrdinalIgnoreCase);
        var schemaTemplates = tables
            .Where(table => table.StartsWith(SchemaTemplatePrefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                table => table[SchemaTemplatePrefix.Length..],
                table => table,
                StringComparer.OrdinalIgnoreCase);
        var missingFixed = FixedTables
            .Where(table => !fixedTemplates.ContainsKey(table))
            .ToArray();
        if (missingFixed.Length > 0
            || !tables.Contains(FixedTableCatalog, StringComparer.OrdinalIgnoreCase))
        {
            await ExecuteAsync(connection, $"DROP DATABASE `{targetDatabase}`;", cancellationToken);
            var missing = missingFixed.Length > 0
                ? string.Join(", ", missingFixed)
                : FixedTableCatalog;
            return new CompanyDatabaseServiceResult(false, "Tabelle fisse non trovate nel master: " + missing + ".");
        }

        var targetNames = fixedTemplates.Keys
            .Concat(schemaTemplates.Keys)
            .Append(FixedTableCatalog)
            .ToArray();
        var duplicateTarget = targetNames
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateTarget is not null)
        {
            await ExecuteAsync(connection, $"DROP DATABASE `{targetDatabase}`;", cancellationToken);
            return new CompanyDatabaseServiceResult(
                false,
                $"Tabella modello duplicata nel master: {duplicateTarget.Key}.");
        }

        try
        {
            await ExecuteAsync(
                connection,
                $"CREATE TABLE `{targetDatabase}`.`{FixedTableCatalog}` LIKE `{sourceDatabase}`.`{FixedTableCatalog}`;",
                cancellationToken);
            await ExecuteAsync(
                connection,
                $"INSERT INTO `{targetDatabase}`.`{FixedTableCatalog}` SELECT * FROM `{sourceDatabase}`.`{FixedTableCatalog}`;",
                cancellationToken);

            foreach (var template in fixedTemplates.Concat(schemaTemplates))
            {
                await ExecuteAsync(
                    connection,
                    $"CREATE TABLE `{targetDatabase}`.`{template.Key}` LIKE `{sourceDatabase}`.`{template.Value}`;",
                    cancellationToken);
            }

            foreach (var template in fixedTemplates)
            {
                await ExecuteAsync(
                    connection,
                    $"INSERT INTO `{targetDatabase}`.`{template.Key}` SELECT * FROM `{sourceDatabase}`.`{template.Value}`;",
                    cancellationToken);
            }

            var masterVersion = await ReadMasterSchemaVersionAsync(connection, cancellationToken);
            await using var versionCommand = new MySqlCommand(
                """
                UPDATE Aziende
                SET VersioneDbAttuale = @version,
                    VersioneDbRichiesta = @version
                WHERE Codice = @code;
                """,
                connection);
            versionCommand.Parameters.AddWithValue("@version", masterVersion);
            versionCommand.Parameters.AddWithValue("@code", code);
            await versionCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            await ExecuteAsync(connection, $"DROP DATABASE `{targetDatabase}`;", cancellationToken);
            throw;
        }

        return new CompanyDatabaseServiceResult(true, $"Database {targetDatabase} creato.");
    }

    public async Task<CompanyDatabaseServiceResult> DeleteCompanyDatabaseAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        var company = await GetCompanyEditAsync(code, cancellationToken);
        if (company is null)
        {
            return new CompanyDatabaseServiceResult(false, "Azienda non trovata.");
        }

        var databaseName = string.IsNullOrWhiteSpace(company.DatabaseName)
            ? options.BuildCompanyDatabaseName(code)
            : company.DatabaseName.Trim();
        var defaultDatabaseName = options.BuildCompanyDatabaseName(MicronoteDatabaseOptions.DefaultCompanyCode);
        if (code == MicronoteDatabaseOptions.DefaultCompanyCode
            || string.Equals(databaseName, defaultDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            return new CompanyDatabaseServiceResult(false, "Il database di riferimento 0001 non può essere eliminato.");
        }

        if (!await DatabaseExistsAsync(databaseName, cancellationToken))
        {
            return new CompanyDatabaseServiceResult(false, $"Il database {databaseName} non esiste.");
        }

        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        await ExecuteAsync(connection, $"DROP DATABASE `{databaseName}`;", cancellationToken);
        return new CompanyDatabaseServiceResult(true, $"Database {databaseName} eliminato.");
    }
    public async Task<CompanyMasterDeleteResult> DeleteCompanyAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "DELETE FROM Aziende WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);
        var changed = await command.ExecuteNonQueryAsync(cancellationToken);
        return changed == 1
            ? new CompanyMasterDeleteResult(true, "Azienda eliminata.")
            : new CompanyMasterDeleteResult(false, "Azienda non trovata.");
    }

    private async Task<bool> CompanyCodeExistsAsync(
        int code,
        CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM Aziende WHERE Codice = @code;",
            connection);
        command.Parameters.AddWithValue("@code", code);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }
    private async Task<bool> CompanyNameExistsAsync(
        string name,
        int? excludeCode,
        CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        var sql = "SELECT COUNT(*) FROM Aziende WHERE LOWER(Nome) = LOWER(@name)";
        if (excludeCode is not null)
        {
            sql += " AND Codice <> @excludeCode";
        }

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@name", name.Trim());
        if (excludeCode is not null)
        {
            command.Parameters.AddWithValue("@excludeCode", excludeCode.Value);
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static void AddCompanyParameters(
        MySqlCommand command,
        CompanyMasterEditModel company)
    {
        command.Parameters.AddWithValue("@code", company.Code);
        command.Parameters.AddWithValue("@name", company.Name.Trim());
        command.Parameters.AddWithValue("@password", company.Password.Trim());
        command.Parameters.AddWithValue("@active", company.Active ? 1 : 0);
        command.Parameters.AddWithValue("@locked", company.Locked ? 1 : 0);
        command.Parameters.AddWithValue("@databaseName", company.DatabaseName.Trim());
        command.Parameters.AddWithValue(
            "@currentVersion",
            company.CurrentDatabaseVersion ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "@requiredVersion",
            company.RequiredDatabaseVersion ?? (object)DBNull.Value);
    }

    private static object DbText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    private async Task<bool> DatabaseExistsAsync(
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenMasterConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT COUNT(*)
            FROM information_schema.SCHEMATA
            WHERE SCHEMA_NAME = @databaseName;
            """,
            connection);
        command.Parameters.AddWithValue("@databaseName", databaseName);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private async Task<bool> CompanyDatabaseIsReadyAsync(
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenCompanyConnectionAsync(databaseName, cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT COUNT(*)
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME IN ('Utenti', 'Opzioni');
            """,
            connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 2;
    }

    private static async Task ExecuteAsync(
        MySqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<DateTime> ReadMasterSchemaVersionAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT VersioneSchemaDatabase FROM Parametri WHERE Chiave = @key LIMIT 1;",
            connection);
        command.Parameters.AddWithValue("@key", DatabaseSchemaVersionKey);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null or DBNull)
        {
            throw new InvalidOperationException(
                "Versione dello schema database non configurata nel master.");
        }

        return Convert.ToDateTime(value);
    }

    private static DateTime? DateTimeValue(MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static async Task<bool> TableExistsAsync(
        MySqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            """
            SELECT COUNT(*)
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @tableName;
            """,
            connection);
        command.Parameters.AddWithValue("@tableName", tableName);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
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
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND LOWER(TABLE_NAME) = LOWER(@tableName)
              AND LOWER(COLUMN_NAME) = LOWER(@columnName);
            """,
            connection);
        command.Parameters.AddWithValue("@tableName", tableName);
        command.Parameters.AddWithValue("@columnName", columnName);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private static async Task<IReadOnlyList<string>> ListTablesAsync(
        MySqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = @databaseName ORDER BY TABLE_NAME;",
            connection);
        command.Parameters.AddWithValue("@databaseName", databaseName);

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(Convert.ToString(reader["TABLE_NAME"]) ?? "");
        }

        return tables.Where(table => !string.IsNullOrWhiteSpace(table)).ToArray();
    }

    private static string ResolveTableName(
        HashSet<string> availableTables,
        string tableName)
    {
        var resolved = availableTables.FirstOrDefault(table =>
            string.Equals(table, tableName, StringComparison.OrdinalIgnoreCase));
        return resolved ?? tableName;
    }
    private CompanyMasterRecord ReadCompany(MySqlDataReader reader)
    {
        var code = Convert.ToInt32(reader["Codice"]);
        var databaseName = Text(reader, "NomeDatabase");
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            databaseName = options.BuildCompanyDatabaseName(code);
        }


        return new CompanyMasterRecord(
            code,
            Convert.ToString(reader["Nome"]) ?? "",
            Convert.ToString(reader["Password"]) ?? "",
            Convert.ToBoolean(reader["Attiva"]),
            Convert.ToBoolean(reader["Bloccata"]),
            databaseName,
            DateTimeValue(reader, "VersioneDbAttuale"),
            DateTimeValue(reader, "VersioneDbRichiesta"));
    }

    private static string? Text(MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
    }
}









