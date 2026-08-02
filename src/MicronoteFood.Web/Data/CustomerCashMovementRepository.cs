using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class CustomerCashMovementRepository(MicronoteDb database)
{
    public async Task<CustomerCashMovementMaskModel> GetMaskAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione
            FROM CausaliCassa
            WHERE COALESCE(Ditta, '') = 'C'
            ORDER BY Descrizione, Codice;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        var causes = new List<CustomerCashMovementCauseOption>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                causes.Add(new(
                    Convert.ToInt32(reader["Codice"]),
                    Convert.ToString(reader["Descrizione"]) ?? ""));
            }
        }

        const string storesSql = "SELECT Codice, COALESCE(Nome, '') AS Nome FROM PuntiVendita ORDER BY Codice;";
        await using var storesCommand = new MySqlCommand(storesSql, connection);
        await using var storesReader = await storesCommand.ExecuteReaderAsync(cancellationToken);
        var stores = new List<CustomerCashMovementStoreOption>();
        while (await storesReader.ReadAsync(cancellationToken))
        {
            stores.Add(new(
                Convert.ToInt32(storesReader["Codice"]),
                Convert.ToString(storesReader["Nome"]) ?? ""));
        }

        return new CustomerCashMovementMaskModel { Causes = causes, Stores = stores };
    }

    public async Task<IReadOnlyList<CustomerCashMovementDocumentRow>> ListDocumentsAsync(
        string documentType,
        int customerCode,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (documentType != "B" || customerCode <= 0 || year <= 0)
            return [];

        const string sql = """
            SELECT ID, Anno, Codice, NumDoc, DataDoc, COALESCE(Totale, 0) AS Totale
            FROM Vendite
            WHERE Cliente = @customer AND Anno = @year AND DataDoc IS NOT NULL
            ORDER BY DataDoc DESC, Codice DESC, ID DESC;
            """;
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@customer", customerCode);
        command.Parameters.AddWithValue("@year", year);
        var rows = new List<CustomerCashMovementDocumentRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                Convert.ToInt32(reader["ID"]),
                Convert.ToInt32(reader["Anno"]),
                Convert.ToInt32(reader["Codice"]),
                Convert.ToInt32(reader["NumDoc"]),
                DateOnly.FromDateTime(Convert.ToDateTime(reader["DataDoc"])),
                Convert.ToDecimal(reader["Totale"])));
        }
        return rows;
    }
    public async Task<CustomerCashMovementEditModel?> GetAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT m.ID, m.Anno, m.Settore, m.Codice, m.DataMov, m.Ditta,
                   COALESCE(c.Nome, '') AS ClienteNome, COALESCE(m.PuntoV, 0) AS ClientePuntoV,
                   m.Causale, m.Importo, m.ModoPag, COALESCE(m.TipoDocumento, '') AS TipoDocumento, m.Documento,
                   v.Anno AS DocumentoAnno, v.Codice AS DocumentoCodice,
                   v.NumDoc AS DocumentoNumero, v.DataDoc AS DocumentoData,
                   COALESCE(m.Annotazioni, '') AS Descrizione
            FROM MovCassa m
            LEFT JOIN Clienti c ON c.Codice = m.Ditta
            LEFT JOIN Vendite v ON m.TipoDocumento = 'B' AND v.ID = m.Documento
            WHERE m.ID = @id AND m.CliFor = 'C'
            LIMIT 1;
            """;

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new CustomerCashMovementEditModel
        {
            Id = Convert.ToInt32(reader["ID"]),
            Code = Convert.ToInt32(reader["Codice"]),
            IsNew = false,
            Year = Convert.ToInt32(reader["Anno"]),
            Sector = Convert.ToInt32(reader["Settore"]),
            MovementDate = DateOnly.FromDateTime(Convert.ToDateTime(reader["DataMov"])),
            CustomerCode = Convert.ToInt32(reader["Ditta"]),
            CustomerName = Convert.ToString(reader["ClienteNome"]) ?? "",
            CustomerStoreCode = Convert.ToInt32(reader["ClientePuntoV"]),
            CauseCode = Convert.ToInt32(reader["Causale"]),
            Amount = reader["Importo"] is DBNull ? 0 : Convert.ToDecimal(reader["Importo"]),
            PaymentMethod = reader["ModoPag"] is DBNull ? 0 : Convert.ToInt32(reader["ModoPag"]),
            DocumentType = Convert.ToString(reader["TipoDocumento"]) ?? "",
            DocumentId = reader["Documento"] is DBNull || Convert.ToInt32(reader["Documento"]) <= 0
                ? null : Convert.ToInt32(reader["Documento"]),
            DocumentYear = reader["DocumentoAnno"] is DBNull
                ? null : Convert.ToInt32(reader["DocumentoAnno"]),
            DocumentCode = reader["DocumentoCodice"] is DBNull
                ? null : Convert.ToInt32(reader["DocumentoCodice"]),
            DocumentNumber = reader["DocumentoNumero"] is DBNull
                ? null : Convert.ToInt32(reader["DocumentoNumero"]),
            DocumentDate = reader["DocumentoData"] is DBNull
                ? null : DateOnly.FromDateTime(Convert.ToDateTime(reader["DocumentoData"])),
            Description = Convert.ToString(reader["Descrizione"]) ?? ""
        };
    }

    public async Task<CustomerCashMovementSaveResult> SaveAsync(
        CustomerCashMovementEditModel movement,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        if (!await ExistsAsync(connection, "Clienti", movement.CustomerCode, cancellationToken))
            return new(false, movement.Id, "Cliente inesistente.");
        if (!await CustomerCauseExistsAsync(connection, movement.CauseCode, cancellationToken))
            return new(false, movement.Id, "Causale contabile cliente inesistente.");
        if (movement.DocumentId is > 0
            && !await CustomerDocumentExistsAsync(
                connection,
                movement.DocumentId.Value,
                movement.CustomerCode,
                cancellationToken))
            return new(false, movement.Id, "La bolla collegata non appartiene al cliente selezionato.");

        if (movement.IsNew || movement.Id <= 0)
        {
            await using var transaction = await connection.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken);
            movement.Code = await NextCodeAsync(
                connection,
                transaction,
                movement.Year,
                cancellationToken);
            const string insert = """
                INSERT INTO MovCassa
                    (Anno, Settore, Codice, DataMov, Causale, TipoMov, CliFor, Ditta,
                     Importo, ModoPag, TipoDocumento, Documento, PuntoV, Annotazioni)
                VALUES
                    (@year, 40, @code, @date, @cause, 'E', 'C', @customer,
                     @amount, @payment, @documentType, @document, @store, @description);
                """;
            await using var command = Command(insert, connection, movement, transaction);
            command.Parameters.AddWithValue("@code", movement.Code);
            await command.ExecuteNonQueryAsync(cancellationToken);
            movement.Id = checked((int)command.LastInsertedId);
            await transaction.CommitAsync(cancellationToken);
            return new(true, movement.Id);
        }

        const string update = """
            UPDATE MovCassa
            SET Anno = @year, Settore = 40, DataMov = @date, Causale = @cause,
                TipoMov = 'E', CliFor = 'C', Ditta = @customer, Importo = @amount,
                ModoPag = @payment, TipoDocumento = @documentType, Documento = @document, PuntoV = @store, Annotazioni = @description
            WHERE ID = @id AND CliFor = 'C';
            """;
        await using var updateCommand = Command(update, connection, movement);
        updateCommand.Parameters.AddWithValue("@id", movement.Id);
        var affected = await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        return affected == 1
            ? new(true, movement.Id)
            : new(false, movement.Id, "Movimento contabile cliente non trovato.");
    }

    private static MySqlCommand Command(
        string sql,
        MySqlConnection connection,
        CustomerCashMovementEditModel movement,
        MySqlTransaction? transaction = null)
    {
        var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@year", movement.Year);
        command.Parameters.Add("@date", MySqlDbType.DateTime).Value = movement.MovementDate.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@cause", movement.CauseCode);
        command.Parameters.AddWithValue("@customer", movement.CustomerCode);
        command.Parameters.AddWithValue("@amount", movement.Amount);
        command.Parameters.AddWithValue("@payment", movement.PaymentMethod);
        command.Parameters.AddWithValue("@documentType", !string.IsNullOrEmpty(movement.DocumentType)
            ? movement.DocumentType : DBNull.Value);
        command.Parameters.AddWithValue("@document", movement.DocumentId is > 0
            ? movement.DocumentId.GetValueOrDefault() : DBNull.Value);
        command.Parameters.AddWithValue("@store", movement.CustomerStoreCode);
        command.Parameters.AddWithValue("@description", movement.Description?.Trim() ?? "");
        return command;
    }

    private static async Task<int> NextCodeAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int year,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(MAX(Codice), 0) + 1
            FROM MovCassa
            WHERE Anno = @year
            FOR UPDATE;
            """;
        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@year", year);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<bool> ExistsAsync(
        MySqlConnection connection,
        string table,
        int code,
        CancellationToken cancellationToken)
    {
        var sql = $"SELECT 1 FROM {table} WHERE Codice = @code LIMIT 1;";
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", code);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<bool> CustomerCauseExistsAsync(
        MySqlConnection connection,
        int code,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1 FROM CausaliCassa
            WHERE Codice = @code AND COALESCE(Ditta, '') = 'C'
            LIMIT 1;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", code);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<bool> CustomerDocumentExistsAsync(
        MySqlConnection connection,
        int documentId,
        int customerCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1 FROM Vendite
            WHERE ID = @documentId AND Cliente = @customerCode
            LIMIT 1;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@documentId", documentId);
        command.Parameters.AddWithValue("@customerCode", customerCode);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }
}
