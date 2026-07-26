using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class AccountingMovementRepository(MicronoteDb database)
{
    public async Task<AccountingMovementEditModel> NewEditModelAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        return new AccountingMovementEditModel
        {
            IsNew = true,
            Year = year,
            Sector = 40,
            Code = await NextCodeAsync(year, cancellationToken),
            MovementDate = DefaultMovementDate(year)
        };
    }

    public async Task<AccountingMovementEditModel?> GetEditModelAsync(
        int id,
        int year,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        const string sql = """
            SELECT m.ID,
                   m.Anno,
                   m.Settore,
                   m.Codice,
                   COALESCE(m.Documento, '') AS Documento,
                   m.DataMov,
                   COALESCE(m.Causale, 0) AS Causale,
                   COALESCE(c.Descrizione, '') AS CausaleDescrizione,
                   COALESCE(m.CliFor, '') AS CliFor,
                   COALESCE(m.Ditta, 0) AS Ditta,
                   CASE
                       WHEN m.CliFor = 'C' THEN COALESCE(cli.Nome, '')
                       WHEN m.CliFor = 'F' THEN COALESCE(forn.Nome, '')
                       WHEN m.CliFor = 'D' THEN TRIM(CONCAT(COALESCE(dip.Cognome, ''), ' ', COALESCE(dip.Nome, '')))
                       WHEN m.CliFor = 'B' THEN COALESCE(ct.Descrizione, '')
                       ELSE ''
                   END AS Nome,
                   COALESCE(m.NumDoc, '') AS NumDoc,
                   COALESCE(m.PuntoV, 0) AS PuntoV,
                   COALESCE(m.Note, '') AS Note,
                   COALESCE(m.Importo, 0) AS Importo
            FROM movcont m
            LEFT JOIN causalicont c ON c.Codice = m.Causale
            LEFT JOIN clienti cli ON cli.Codice = m.Ditta
            LEFT JOIN fornitori forn ON forn.Codice = m.Ditta
            LEFT JOIN dipendenti dip ON dip.Codice = m.Ditta
            LEFT JOIN conti ct ON ct.Codice = m.Ditta
            WHERE m.ID = @id
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var model = new AccountingMovementEditModel
        {
            IsNew = false,
            Id = Convert.ToInt32(reader["ID"]),
            Year = Convert.ToInt32(reader["Anno"]),
            Sector = Convert.ToInt32(reader["Settore"]),
            Code = Convert.ToInt32(reader["Codice"]),
            MovementDate = DateOnly.FromDateTime(Convert.ToDateTime(reader["DataMov"])),
            CauseCode = ToNullableCode(reader["Causale"]),
            CauseDescription = Convert.ToString(reader["CausaleDescrizione"]) ?? "",
            SubjectType = Convert.ToString(reader["CliFor"]) ?? "",
            SubjectCode = ToNullableCode(reader["Ditta"]),
            SubjectName = Convert.ToString(reader["Nome"]) ?? "",
            DocumentNumber = Convert.ToString(reader["NumDoc"]) ?? "",
            StoreCode = ToNullableCode(reader["PuntoV"]),
            Notes = Convert.ToString(reader["Note"]) ?? "",
            Amount = Money(reader["Importo"])
        };

        await reader.DisposeAsync();
        await LoadLinesAsync(connection, model, cancellationToken);
        await LoadLinkedDocumentAsync(connection, model, cancellationToken);
        return model;
    }

    public async Task<int> SaveAsync(
        AccountingMovementEditModel movement,
        int currentYear,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var id = movement.IsNew || movement.Id is null
                ? await InsertMovementAsync(connection, transaction, movement, currentYear, cancellationToken)
                : await UpdateMovementAsync(connection, transaction, movement, cancellationToken);

            await ReplaceLinesAsync(connection, transaction, id, movement, cancellationToken);
            await ReplaceLinkedDocumentAsync(connection, transaction, id, movement, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return id;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }



    public async Task<bool> DeleteMovementAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using (var linkedCommand = new MySqlCommand(
                "DELETE FROM movcontdc WHERE Mov_Id = @id;",
                connection,
                transaction))
            {
                linkedCommand.Parameters.AddWithValue("@id", id);
                await linkedCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var linesCommand = new MySqlCommand(
                "DELETE FROM movcontrg WHERE ID = @id;",
                connection,
                transaction))
            {
                linesCommand.Parameters.AddWithValue("@id", id);
                await linesCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var movementCommand = new MySqlCommand(
                "DELETE FROM movcont WHERE ID = @id;",
                connection,
                transaction);
            movementCommand.Parameters.AddWithValue("@id", id);
            var affected = await movementCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return affected > 0;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
    public async Task<bool> DeleteBankMovementAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using (var linkedCommand = new MySqlCommand(
                "DELETE FROM movcontdc WHERE Mov_Id = @id;",
                connection,
                transaction))
            {
                linkedCommand.Parameters.AddWithValue("@id", id);
                await linkedCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var linesCommand = new MySqlCommand(
                "DELETE FROM movcontrg WHERE ID = @id;",
                connection,
                transaction))
            {
                linesCommand.Parameters.AddWithValue("@id", id);
                await linesCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var movementCommand = new MySqlCommand(
                "DELETE FROM movcont WHERE ID = @id AND Settore = 60;",
                connection,
                transaction);
            movementCommand.Parameters.AddWithValue("@id", id);
            var affected = await movementCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return affected > 0;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<AccountingArticleDetailModel?> GetArticleDetailAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        const string headerSql = """
            SELECT m.ID,
                   m.Anno,
                   m.Codice,
                   m.DataMov,
                   COALESCE(m.Causale, 0) AS Causale,
                   COALESCE(c.Descrizione, '') AS CausaleDescrizione,
                   COALESCE(m.CliFor, '') AS CliFor,
                   COALESCE(m.Ditta, 0) AS Ditta,
                   CASE
                       WHEN m.CliFor = 'C' THEN COALESCE(cli.Nome, '')
                       WHEN m.CliFor = 'F' THEN COALESCE(forn.Nome, '')
                       WHEN m.CliFor = 'D' THEN TRIM(CONCAT(COALESCE(dip.Cognome, ''), ' ', COALESCE(dip.Nome, '')))
                       WHEN m.CliFor = 'B' THEN COALESCE(ct.Descrizione, '')
                       ELSE ''
                   END AS Nome,
                   COALESCE(m.NumDoc, '') AS NumDoc,
                   COALESCE(m.Importo, 0) AS Importo
            FROM movcont m
            LEFT JOIN causalicont c ON c.Codice = m.Causale
            LEFT JOIN clienti cli ON cli.Codice = m.Ditta
            LEFT JOIN fornitori forn ON forn.Codice = m.Ditta
            LEFT JOIN dipendenti dip ON dip.Codice = m.Ditta
            LEFT JOIN conti ct ON ct.Codice = m.Ditta
            WHERE m.ID = @id
            LIMIT 1;
            """;

        await using var headerCommand = new MySqlCommand(headerSql, connection);
        headerCommand.Parameters.AddWithValue("@id", id);

        AccountingArticleDetailModel? model = null;
        await using (var reader = await headerCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                model = new AccountingArticleDetailModel
                {
                    Id = Convert.ToInt32(reader["ID"]),
                    Year = Convert.ToInt32(reader["Anno"]),
                    Code = Convert.ToInt32(reader["Codice"]),
                    MovementDate = DateOnly.FromDateTime(Convert.ToDateTime(reader["DataMov"])),
                    CauseCode = Convert.ToInt32(reader["Causale"]),
                    CauseDescription = Convert.ToString(reader["CausaleDescrizione"]) ?? "",
                    SubjectLabel = SubjectLabel(Convert.ToString(reader["CliFor"]) ?? ""),
                    SubjectCode = ToNullableCode(reader["Ditta"]),
                    SubjectName = Convert.ToString(reader["Nome"]) ?? "",
                    DocumentNumber = Convert.ToString(reader["NumDoc"]) ?? "",
                    Amount = Money(reader["Importo"])
                };
            }
        }

        if (model is null)
        {
            return null;
        }

        const string linesSql = """
            SELECT COALESCE(r.Segno, '') AS Segno,
                   COALESCE(r.Conto, 0) AS Conto,
                   COALESCE(c.Descrizione, '') AS Descrizione,
                   COALESCE(r.Importo, 0) AS Importo
            FROM movcontrg r
            LEFT JOIN conti c ON c.Codice = r.Conto
            WHERE r.ID = @id
            ORDER BY r.Segno DESC, r.Riga ASC;
            """;

        await using var linesCommand = new MySqlCommand(linesSql, connection);
        linesCommand.Parameters.AddWithValue("@id", id);

        var debitLines = new List<AccountingArticleDetailLine>();
        var creditLines = new List<AccountingArticleDetailLine>();
        await using (var reader = await linesCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var line = new AccountingArticleDetailLine(
                    Convert.ToInt32(reader["Conto"]),
                    Convert.ToString(reader["Descrizione"]) ?? "",
                    Money(reader["Importo"]));

                var sign = Convert.ToString(reader["Segno"]) ?? "";
                if (IsDebitSign(sign))
                {
                    debitLines.Add(line);
                }
                else if (IsCreditSign(sign))
                {
                    creditLines.Add(line);
                }
            }
        }

        model.DebitLines = debitLines;
        model.CreditLines = creditLines;
        return model;
    }
    public async Task<AccountingMovementListPageModel> GetListAsync(
        int year,
        DateOnly dateFrom,
        DateOnly dateTo,
        int? causeCode,
        string? movementType,
        int? supplierCode,
        int? selectedId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        var movements = await ListMovementsAsync(
            connection,
            dateFrom,
            dateTo,
            causeCode,
            NormalizeType(movementType),
            supplierCode,
            cancellationToken);
        var selected = SelectMovement(movements, selectedId);

        return new AccountingMovementListPageModel
        {
            Year = year,
            DateFrom = dateFrom,
            DateTo = dateTo,
            CauseCode = causeCode,
            MovementType = NormalizeType(movementType),
            SupplierCode = supplierCode,
            SupplierName = supplierCode is null
                ? ""
                : await SupplierNameAsync(connection, supplierCode.Value, cancellationToken),
            SelectedId = selected?.Id,
            Movements = movements,
            Causes = await ListCausesAsync(connection, cancellationToken),
            MovementTypes = await ListMovementTypesAsync(connection, cancellationToken)
        };
    }


    public async Task<BankMovementListPageModel> GetBankMovementsAsync(
        int currentYear,
        int? year,
        int? month,
        int? bankCode,
        int? causeCode,
        int? selectedId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);

        var selectedYear = year.GetValueOrDefault(currentYear);
        var selectedMonth = month.GetValueOrDefault(0);
        var movements = await ListBankMovementsAsync(
            connection,
            selectedYear,
            selectedMonth,
            bankCode,
            causeCode,
            cancellationToken);
        var selected = SelectBankMovement(movements, selectedId);

        return new BankMovementListPageModel
        {
            Year = selectedYear,
            Month = selectedMonth,
            BankCode = bankCode,
            CauseCode = causeCode,
            SelectedId = selected?.Id,
            Years = BuildYearOptions(currentYear, 6),
            Months = BuildMonthOptions(),
            Banks = await ListBankAccountsAsync(connection, cancellationToken),
            Causes = await ListBankCausesAsync(connection, cancellationToken),
            Movements = movements,
            Total = movements.Sum(movement => movement.Amount)
        };
    }
    public async Task<IReadOnlyList<AccountingMovementCauseTemplate>> ListCauseTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Codice,
                   COALESCE(Descrizione, '') AS Descrizione,
                   COALESCE(TipoMov, '') AS TipoMov,
                   COALESCE(CliFor, '') AS CliFor,
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
                   COALESCE(Avere6, 0) AS Avere6,
                   COALESCE(Fattura, 0) AS Fattura,
                   COALESCE(Scadenza, 0) AS Scadenza
            FROM causalicont
            ORDER BY Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var rows = new List<AccountingMovementCauseTemplate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new AccountingMovementCauseTemplate(
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Descrizione"]) ?? "",
                Convert.ToString(reader["TipoMov"]) ?? "",
                Convert.ToString(reader["CliFor"]) ?? "",
                ToNullableCode(reader["Dare1"]),
                ToNullableCode(reader["Dare2"]),
                ToNullableCode(reader["Dare3"]),
                ToNullableCode(reader["Dare4"]),
                ToNullableCode(reader["Dare5"]),
                ToNullableCode(reader["Dare6"]),
                ToNullableCode(reader["Avere1"]),
                ToNullableCode(reader["Avere2"]),
                ToNullableCode(reader["Avere3"]),
                ToNullableCode(reader["Avere4"]),
                ToNullableCode(reader["Avere5"]),
                ToNullableCode(reader["Avere6"]),
                ToBool(reader["Fattura"]),
                ToBool(reader["Scadenza"])));
        }

        return rows;
    }

    public async Task<IReadOnlyList<AccountingMovementStoreOption>> ListStoresAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Codice, COALESCE(Nome, '') AS Nome
            FROM puntivendita
            ORDER BY Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        var rows = new List<AccountingMovementStoreOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new AccountingMovementStoreOption(
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Nome"]) ?? ""));
        }

        return rows;
    }

    public async Task<IReadOnlyList<AccountingLinkedDocumentOption>> ListLinkedDocumentsAsync(
        string kind,
        int year,
        string subjectType,
        int subjectCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedKind = (kind ?? "").Trim().ToLowerInvariant();
        var normalizedSubject = (subjectType ?? "").Trim().ToUpperInvariant();
        if (subjectCode <= 0 || string.IsNullOrWhiteSpace(normalizedSubject))
        {
            return [];
        }

        var sector = LinkedDocumentSector(normalizedKind, normalizedSubject);
        if (sector <= 0)
        {
            return [];
        }

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return normalizedKind switch
        {
            "invoice" => await ListLinkedInvoicesAsync(
                connection,
                year,
                sector,
                normalizedSubject,
                subjectCode,
                cancellationToken),
            "due-date" => await ListLinkedDueDatesAsync(
                connection,
                year,
                sector,
                normalizedSubject,
                subjectCode,
                cancellationToken),
            _ => []
        };
    }

    private async Task<int> NextCodeAsync(
        int year,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(MAX(Codice), 0) + 1
            FROM movcont
            WHERE Anno = @year;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> NextCodeAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(MAX(Codice), 0) + 1
            FROM movcont
            WHERE Anno = @year;
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@year", year);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> InsertMovementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        AccountingMovementEditModel movement,
        int currentYear,
        CancellationToken cancellationToken)
    {
        movement.Year = currentYear;
        movement.Sector = movement.Sector <= 0 ? 40 : movement.Sector;
        movement.Code = await NextCodeAsync(connection, transaction, movement.Year, cancellationToken);

        const string sql = """
            INSERT INTO movcont
                (Anno, Settore, Codice, Causale, DataMov, CliFor, Ditta, NumDoc,
                 TipoPag, Titolo, Documento, Importo, PuntoV, Note)
            VALUES
                (@year, @sector, @code, @cause, @movementDate, @subjectType, @subjectCode, @documentNumber,
                 '', '', NULL, @amount, @storeCode, @notes);
            SELECT LAST_INSERT_ID();
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        AddMovementParameters(command, movement);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> UpdateMovementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        AccountingMovementEditModel movement,
        CancellationToken cancellationToken)
    {
        const string loadSql = """
            SELECT Anno, Settore, Codice, Causale
            FROM movcont
            WHERE ID = @id
            LIMIT 1;
            """;

        await using (var loadCommand = new MySqlCommand(loadSql, connection, transaction))
        {
            loadCommand.Parameters.AddWithValue("@id", movement.Id!.Value);
            await using var reader = await loadCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Movimento contabile non trovato.");
            }

            movement.Year = Convert.ToInt32(reader["Anno"]);
            movement.Sector = Convert.ToInt32(reader["Settore"]);
            movement.Code = Convert.ToInt32(reader["Codice"]);
            movement.CauseCode = ToNullableCode(reader["Causale"]);
        }

        const string sql = """
            UPDATE movcont
            SET DataMov = @movementDate,
                CliFor = @subjectType,
                Ditta = @subjectCode,
                NumDoc = @documentNumber,
                Importo = @amount,
                PuntoV = @storeCode,
                Note = @notes
            WHERE ID = @id;
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@id", movement.Id!.Value);
        AddMovementParameters(command, movement);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return movement.Id.Value;
    }

    private static async Task ReplaceLinesAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int movementId,
        AccountingMovementEditModel movement,
        CancellationToken cancellationToken)
    {
        await using (var deleteCommand = new MySqlCommand(
            "DELETE FROM movcontrg WHERE ID = @id;",
            connection,
            transaction))
        {
            deleteCommand.Parameters.AddWithValue("@id", movementId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertLinesForSideAsync(
            connection,
            transaction,
            movementId,
            movement,
            movement.DebitLines,
            "D",
            cancellationToken);
        await InsertLinesForSideAsync(
            connection,
            transaction,
            movementId,
            movement,
            movement.CreditLines,
            "A",
            cancellationToken);
    }

    private static async Task InsertLinesForSideAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int movementId,
        AccountingMovementEditModel movement,
        IEnumerable<AccountingMovementLineEditModel> lines,
        string sign,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO movcontrg
                (ID, Anno, Settore, Codice, Riga, Conto, Importo, Segno)
            VALUES
                (@id, @year, @sector, @code, @rowNumber, @accountCode, @amount, @sign);
            """;

        var rowNumber = 1;
        foreach (var line in CompactLines(lines))
        {
            await using var command = new MySqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@id", movementId);
            command.Parameters.AddWithValue("@year", movement.Year);
            command.Parameters.AddWithValue("@sector", movement.Sector);
            command.Parameters.AddWithValue("@code", movement.Code);
            command.Parameters.AddWithValue("@rowNumber", rowNumber++);
            command.Parameters.AddWithValue("@accountCode", line.AccountCode!.Value);
            command.Parameters.AddWithValue("@amount", line.Amount);
            command.Parameters.AddWithValue("@sign", sign);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static IEnumerable<AccountingMovementLineEditModel> CompactLines(
        IEnumerable<AccountingMovementLineEditModel> lines) =>
        lines.Where(line => line.AccountCode.GetValueOrDefault() > 0 || line.Amount != 0)
            .Where(line => line.AccountCode.GetValueOrDefault() > 0 && line.Amount != 0);

    private static void AddMovementParameters(
        MySqlCommand command,
        AccountingMovementEditModel movement)
    {
        command.Parameters.AddWithValue("@year", movement.Year);
        command.Parameters.AddWithValue("@sector", movement.Sector);
        command.Parameters.AddWithValue("@code", movement.Code);
        command.Parameters.AddWithValue("@cause", movement.CauseCode is null ? DBNull.Value : movement.CauseCode.Value);
        command.Parameters.AddWithValue("@movementDate", movement.MovementDate.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@subjectType", string.IsNullOrWhiteSpace(movement.SubjectType) ? DBNull.Value : movement.SubjectType.Trim());
        command.Parameters.AddWithValue("@subjectCode", movement.SubjectCode is null ? DBNull.Value : movement.SubjectCode.Value);
        command.Parameters.AddWithValue("@documentNumber", string.IsNullOrWhiteSpace(movement.DocumentNumber) ? DBNull.Value : movement.DocumentNumber.Trim());
        command.Parameters.AddWithValue("@amount", movement.Amount);
        command.Parameters.AddWithValue("@storeCode", movement.StoreCode is null ? DBNull.Value : movement.StoreCode.Value);
        command.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(movement.Notes) ? DBNull.Value : movement.Notes.Trim());
    }

    private static async Task LoadLinesAsync(
        MySqlConnection connection,
        AccountingMovementEditModel model,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Segno,
                   Riga,
                   Conto,
                   COALESCE(Importo, 0) AS Importo
            FROM movcontrg
            WHERE ID = @id
            ORDER BY Segno DESC, Riga ASC;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", model.Id);

        var debitIndex = 0;
        var creditIndex = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var sign = Convert.ToString(reader["Segno"]) ?? "";
            AccountingMovementLineEditModel? line = null;
            if (IsDebitSign(sign) && debitIndex < model.DebitLines.Count)
            {
                line = model.DebitLines[debitIndex++];
            }
            else if (IsCreditSign(sign) && creditIndex < model.CreditLines.Count)
            {
                line = model.CreditLines[creditIndex++];
            }

            if (line is null)
            {
                continue;
            }

            line.AccountCode = ToNullableCode(reader["Conto"]);
            line.Amount = Money(reader["Importo"]);
        }
    }

    private static async Task LoadLinkedDocumentAsync(
        MySqlConnection connection,
        AccountingMovementEditModel model,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Settore,
                   Doc_Id,
                   COALESCE(TipoDoc, '') AS TipoDoc
            FROM movcontdc
            WHERE Mov_Id = @id
            ORDER BY TipoDoc;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", model.Id);

        var linkedRows = new List<(string Type, int? Sector, int? DocumentId)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var type = Convert.ToString(reader["TipoDoc"])?.Trim() ?? "";
            var sector = ToNullableCode(reader["Settore"]);
            var documentId = ToNullableCode(reader["Doc_Id"]);
            linkedRows.Add((type, sector, documentId));
        }

        await reader.DisposeAsync();
        foreach (var (type, sector, documentId) in linkedRows)
        {
            if (string.Equals(type, "Fattura", StringComparison.OrdinalIgnoreCase))
            {
                model.LinkedInvoiceDocumentSector = sector;
                model.LinkedInvoiceDocumentId = documentId;
                await LoadLinkedInvoiceDetailAsync(connection, model, cancellationToken);
            }
            else if (string.Equals(type, "Scadenza", StringComparison.OrdinalIgnoreCase))
            {
                model.LinkedDueDateDocumentSector = sector;
                model.LinkedDueDateDocumentId = documentId;
                await LoadLinkedDueDateDetailAsync(connection, model, cancellationToken);
            }
        }
    }

    private static async Task LoadLinkedInvoiceDetailAsync(
        MySqlConnection connection,
        AccountingMovementEditModel model,
        CancellationToken cancellationToken)
    {
        if (model.LinkedInvoiceDocumentId.GetValueOrDefault() <= 0)
        {
            return;
        }

        const string sql = """
            SELECT COALESCE(NumDoc, '') AS NumDoc,
                   DataDoc
            FROM moviva
            WHERE ID = @id
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", model.LinkedInvoiceDocumentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            model.LinkedInvoiceDocumentNumber = Convert.ToString(reader["NumDoc"]) ?? "";
            model.LinkedInvoiceDocumentDate = Date(reader["DataDoc"]);
        }
    }

    private static async Task LoadLinkedDueDateDetailAsync(
        MySqlConnection connection,
        AccountingMovementEditModel model,
        CancellationToken cancellationToken)
    {
        if (model.LinkedDueDateDocumentId.GetValueOrDefault() <= 0)
        {
            return;
        }

        const string sql = """
            SELECT COALESCE(NumeroFatt, '') AS NumDoc,
                   DataFatt
            FROM scadenze
            WHERE ID = @id
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", model.LinkedDueDateDocumentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            model.LinkedDueDateDocumentNumber = Convert.ToString(reader["NumDoc"]) ?? "";
            model.LinkedDueDateDocumentDate = Date(reader["DataFatt"]);
        }
    }

    private static async Task ReplaceLinkedDocumentAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int movementId,
        AccountingMovementEditModel movement,
        CancellationToken cancellationToken)
    {
        await using (var deleteCommand = new MySqlCommand(
            "DELETE FROM movcontdc WHERE Mov_Id = @id;",
            connection,
            transaction))
        {
            deleteCommand.Parameters.AddWithValue("@id", movementId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertLinkedDocumentAsync(
            connection,
            transaction,
            movementId,
            movement.LinkedInvoiceDocumentSector,
            movement.LinkedInvoiceDocumentId,
            "Fattura",
            cancellationToken);

        await InsertLinkedDocumentAsync(
            connection,
            transaction,
            movementId,
            movement.LinkedDueDateDocumentSector,
            movement.LinkedDueDateDocumentId,
            "Scadenza",
            cancellationToken);
    }

    private static async Task InsertLinkedDocumentAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int movementId,
        int? sector,
        int? documentId,
        string documentType,
        CancellationToken cancellationToken)
    {
        var linkedDocumentId = documentId.GetValueOrDefault();
        var linkedDocumentSector = sector.GetValueOrDefault();
        if (linkedDocumentId <= 0 || linkedDocumentSector <= 0)
        {
            return;
        }

        await using var insertCommand = new MySqlCommand(
            """
            INSERT INTO movcontdc
                (Mov_Id, Settore, Doc_Id, TipoDoc)
            VALUES
                (@movementId, @sector, @documentId, @documentType);
            """,
            connection,
            transaction);
        insertCommand.Parameters.AddWithValue("@movementId", movementId);
        insertCommand.Parameters.AddWithValue("@sector", linkedDocumentSector);
        insertCommand.Parameters.AddWithValue("@documentId", linkedDocumentId);
        insertCommand.Parameters.AddWithValue("@documentType", documentType);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
    }


    private static string SubjectLabel(string value) =>
        value.Trim().ToUpperInvariant() switch
        {
            "C" => "Cliente",
            "F" => "Fornitore",
            "D" => "Dipendente",
            "B" => "Banca",
            _ => "Ditta"
        };
    private static bool IsDebitSign(string sign) =>
        string.Equals(sign.Trim(), "D", StringComparison.OrdinalIgnoreCase)
        || string.Equals(sign.Trim(), "Dare", StringComparison.OrdinalIgnoreCase);

    private static bool IsCreditSign(string sign) =>
        string.Equals(sign.Trim(), "A", StringComparison.OrdinalIgnoreCase)
        || string.Equals(sign.Trim(), "Avere", StringComparison.OrdinalIgnoreCase);

    private static DateOnly DefaultMovementDate(int year)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return today.Year == year ? today : new DateOnly(year, 12, 31);
    }

    private static int? ToNullableCode(object value)
    {
        if (value is null || value == DBNull.Value)
        {
            return null;
        }

        var code = Convert.ToInt32(value);
        return code > 0 ? code : null;
    }

    private static bool ToBool(object value)
    {
        if (value is null || value == DBNull.Value)
        {
            return false;
        }

        return Convert.ToInt32(value) != 0;
    }


    private static BankMovementListItem? SelectBankMovement(
        IReadOnlyList<BankMovementListItem> movements,
        int? selectedId) =>
        selectedId is null
            ? movements.FirstOrDefault()
            : movements.FirstOrDefault(row => row.Id == selectedId.Value)
              ?? movements.FirstOrDefault();

    private static async Task<IReadOnlyList<BankMovementListItem>> ListBankMovementsAsync(
        MySqlConnection connection,
        int year,
        int month,
        int? bankCode,
        int? causeCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT m.ID,
                   m.Anno,
                   m.Codice,
                   m.DataMov,
                   COALESCE(m.Causale, 0) AS Causale,
                   COALESCE(c.Descrizione, '') AS CausaleDescrizione,
                   COALESCE(m.Ditta, 0) AS Banca,
                   COALESCE(ct.Descrizione, '') AS Nome,
                   COALESCE(m.Importo, 0) AS Importo,
                   COALESCE(m.Titolo, '') AS Titolo
            FROM movcont m
            LEFT JOIN causalicont c ON c.Codice = m.Causale
            LEFT JOIN conti ct ON ct.Codice = m.Ditta
            WHERE m.Settore = 60
              AND (@year = 0 OR m.Anno = @year)
              AND (@month = 0 OR MONTH(m.DataMov) = @month)
              AND (@bankCode IS NULL OR m.Ditta = @bankCode)
              AND (@causeCode IS NULL OR m.Causale = @causeCode)
            ORDER BY m.DataMov DESC, m.Codice DESC;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@month", month);
        command.Parameters.AddWithValue("@bankCode", bankCode is null ? DBNull.Value : bankCode.Value);
        command.Parameters.AddWithValue("@causeCode", causeCode is null ? DBNull.Value : causeCode.Value);

        var rows = new List<BankMovementListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new BankMovementListItem(
                Convert.ToInt32(reader["ID"]),
                Convert.ToInt32(reader["Anno"]),
                Convert.ToInt32(reader["Codice"]),
                DateOnly.FromDateTime(Convert.ToDateTime(reader["DataMov"])),
                Convert.ToInt32(reader["Causale"]),
                Convert.ToString(reader["CausaleDescrizione"]) ?? "",
                Convert.ToInt32(reader["Banca"]),
                Convert.ToString(reader["Nome"]) ?? "",
                Money(reader["Importo"]),
                Convert.ToString(reader["Titolo"]) ?? ""));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<BankMovementBankOption>> ListBankAccountsAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione
            FROM conti
            WHERE COALESCE(Ditta, '') = 'B'
              AND COALESCE(Tipo, '') = 'P'
            ORDER BY Descrizione, Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<BankMovementBankOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new BankMovementBankOption(
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Descrizione"]) ?? ""));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<AccountingMovementCauseOption>> ListBankCausesAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione
            FROM causalicont
            WHERE COALESCE(TipoMov, '') = 'B'
            ORDER BY Descrizione, Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<AccountingMovementCauseOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new AccountingMovementCauseOption(
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Descrizione"]) ?? ""));
        }

        return rows;
    }

    private static IReadOnlyList<int> BuildYearOptions(int currentYear, int previousYears) =>
        Enumerable.Range(0, previousYears + 1)
            .Select(offset => currentYear - offset)
            .ToArray();

    private static IReadOnlyList<MonthOption> BuildMonthOptions() =>
    [
        new(0, ""),
        new(1, "Gennaio"),
        new(2, "Febbraio"),
        new(3, "Marzo"),
        new(4, "Aprile"),
        new(5, "Maggio"),
        new(6, "Giugno"),
        new(7, "Luglio"),
        new(8, "Agosto"),
        new(9, "Settembre"),
        new(10, "Ottobre"),
        new(11, "Novembre"),
        new(12, "Dicembre")
    ];
    private static AccountingMovementListItem? SelectMovement(
        IReadOnlyList<AccountingMovementListItem> movements,
        int? selectedId) =>
        selectedId is null
            ? movements.FirstOrDefault()
            : movements.FirstOrDefault(row => row.Id == selectedId.Value)
              ?? movements.FirstOrDefault();

    private static async Task<IReadOnlyList<AccountingMovementListItem>> ListMovementsAsync(
        MySqlConnection connection,
        DateOnly dateFrom,
        DateOnly dateTo,
        int? causeCode,
        string? movementType,
        int? supplierCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT m.ID,
                   m.Anno,
                   m.Settore,
                   m.Codice,
                   COALESCE(m.Documento, '') AS Documento,
                   m.DataMov,
                   COALESCE(m.NumDoc, '') AS NumDoc,
                   m.Causale,
                   COALESCE(c.Descrizione, '') AS CausaleDescrizione,
                   COALESCE(c.TipoMov, '') AS TipoMov,
                   COALESCE(c.Stampa, 0) AS Stampa,
                   COALESCE(m.Importo, 0) AS Importo,
                   COALESCE(m.CliFor, '') AS CliFor,
                   COALESCE(m.Ditta, 0) AS Ditta,
                   CASE
                       WHEN m.CliFor = 'C' THEN COALESCE(cli.Nome, '')
                       WHEN m.CliFor = 'F' THEN COALESCE(forn.Nome, '')
                       WHEN m.CliFor = 'D' THEN TRIM(CONCAT(COALESCE(dip.Cognome, ''), ' ', COALESCE(dip.Nome, '')))
                       WHEN m.CliFor = 'B' THEN COALESCE(ct.Descrizione, '')
                       ELSE ''
                   END AS Nome,
                   COALESCE(m.Note, '') AS Note
            FROM movcont m
            LEFT JOIN causalicont c ON c.Codice = m.Causale
            LEFT JOIN clienti cli ON cli.Codice = m.Ditta
            LEFT JOIN fornitori forn ON forn.Codice = m.Ditta
            LEFT JOIN dipendenti dip ON dip.Codice = m.Ditta
            LEFT JOIN conti ct ON ct.Codice = m.Ditta
            WHERE m.DataMov BETWEEN @dateFrom AND @dateTo
              AND (@causeCode IS NULL OR m.Causale = @causeCode)
              AND (@movementType = '' OR c.TipoMov = @movementType)
              AND (@supplierCode IS NULL OR (m.CliFor = 'F' AND m.Ditta = @supplierCode))
            ORDER BY m.DataMov DESC, m.Codice DESC;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@dateFrom", dateFrom.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@dateTo", dateTo.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@causeCode", causeCode is null ? DBNull.Value : causeCode.Value);
        command.Parameters.AddWithValue("@movementType", movementType ?? "");
        command.Parameters.AddWithValue("@supplierCode", supplierCode is null ? DBNull.Value : supplierCode.Value);

        var rows = new List<AccountingMovementListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new AccountingMovementListItem(
                Convert.ToInt32(reader["ID"]),
                Convert.ToInt32(reader["Anno"]),
                Convert.ToInt32(reader["Settore"]),
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Documento"]) ?? "",
                DateOnly.FromDateTime(Convert.ToDateTime(reader["DataMov"])),
                Convert.ToString(reader["NumDoc"]) ?? "",
                Convert.ToInt32(reader["Causale"]),
                Convert.ToString(reader["CausaleDescrizione"]) ?? "",
                Convert.ToString(reader["TipoMov"]) ?? "",
                Money(reader["Importo"]),
                Convert.ToString(reader["CliFor"]) ?? "",
                Convert.ToInt32(reader["Ditta"]),
                Convert.ToString(reader["Nome"]) ?? "",
                Convert.ToString(reader["Note"]) ?? "",
                Convert.ToBoolean(reader["Stampa"])));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<AccountingMovementCauseOption>> ListCausesAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione
            FROM causalicont
            ORDER BY Descrizione, Codice;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<AccountingMovementCauseOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new AccountingMovementCauseOption(
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["Descrizione"]) ?? ""));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<AccountingMovementTypeOption>> ListMovementTypesAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT COALESCE(TipoMov, '') AS TipoMov
            FROM causalicont
            WHERE COALESCE(TipoMov, '') <> ''
            ORDER BY TipoMov;
            """;

        await using var command = new MySqlCommand(sql, connection);
        var rows = new List<AccountingMovementTypeOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = Convert.ToString(reader["TipoMov"]) ?? "";
            rows.Add(new AccountingMovementTypeOption(code, TypeDescription(code)));
        }

        return rows;
    }

    private static int LinkedDocumentSector(string kind, string subjectType) =>
        kind switch
        {
            "invoice" => subjectType == "C" ? 30 : subjectType == "F" ? 10 : 0,
            "due-date" => subjectType == "C" ? 30 : subjectType == "F" ? 10 : 0,
            _ => 0
        };

    private static async Task<IReadOnlyList<AccountingLinkedDocumentOption>> ListLinkedInvoicesAsync(
        MySqlConnection connection,
        int year,
        int sector,
        string subjectType,
        int subjectCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ID,
                   Anno,
                   Settore,
                   Codice,
                   COALESCE(NumDoc, '') AS NumDoc,
                   DataDoc,
                   COALESCE(Totale, 0) AS Totale
            FROM moviva
            WHERE Anno = @year
              AND Settore = @sector
              AND CliFor = @subjectType
              AND Ditta = @subjectCode
            ORDER BY DataDoc DESC, NumDoc DESC, Codice DESC;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@sector", sector);
        command.Parameters.AddWithValue("@subjectType", subjectType);
        command.Parameters.AddWithValue("@subjectCode", subjectCode);

        var rows = new List<AccountingLinkedDocumentOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new AccountingLinkedDocumentOption(
                Convert.ToInt32(reader["ID"]),
                Convert.ToInt32(reader["Anno"]),
                Convert.ToInt32(reader["Settore"]),
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["NumDoc"]) ?? "",
                Date(reader["DataDoc"]),
                Money(reader["Totale"])));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<AccountingLinkedDocumentOption>> ListLinkedDueDatesAsync(
        MySqlConnection connection,
        int year,
        int sector,
        string subjectType,
        int subjectCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ID,
                   Anno,
                   Settore,
                   Codice,
                   COALESCE(NumeroFatt, '') AS NumDoc,
                   DataFatt,
                   COALESCE(Importo, 0) AS Importo
            FROM scadenze
            WHERE Anno = @year
              AND Settore = @sector
              AND CliFor = @subjectType
              AND Ditta = @subjectCode
            ORDER BY DataScadenza DESC, Codice DESC, Numero DESC;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@sector", sector);
        command.Parameters.AddWithValue("@subjectType", subjectType);
        command.Parameters.AddWithValue("@subjectCode", subjectCode);

        var rows = new List<AccountingLinkedDocumentOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new AccountingLinkedDocumentOption(
                Convert.ToInt32(reader["ID"]),
                Convert.ToInt32(reader["Anno"]),
                Convert.ToInt32(reader["Settore"]),
                Convert.ToInt32(reader["Codice"]),
                Convert.ToString(reader["NumDoc"]) ?? "",
                Date(reader["DataFatt"]),
                Money(reader["Importo"])));
        }

        return rows;
    }

    private static async Task<string> SupplierNameAsync(
        MySqlConnection connection,
        int code,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COALESCE(Nome, '') FROM fornitori WHERE Codice = @code LIMIT 1;",
            connection);
        command.Parameters.AddWithValue("@code", code);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? "";
    }

    private static string? NormalizeType(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string TypeDescription(string code) =>
        code switch
        {
            "V" => "V - Movimenti iva",
            "C" => "C - Movimenti contabili",
            "B" => "B - Movimenti bancari",
            "D" => "D - Movimenti dipendenti",
            _ => code
        };

    private static decimal Money(object value) =>
        value is null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);

    private static DateOnly? Date(object value) =>
        value is null || value == DBNull.Value
            ? null
            : DateOnly.FromDateTime(Convert.ToDateTime(value));
}








