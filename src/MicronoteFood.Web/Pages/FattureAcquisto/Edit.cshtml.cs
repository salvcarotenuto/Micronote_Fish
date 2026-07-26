using System.Globalization;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Pages.FattureAcquisto;

public sealed class EditModel(
    PurchaseInvoiceRepository repository,
    MicronoteDb db,
    ApplicationState applicationState,
    MicronoteServicePaths servicePaths,
    IWebHostEnvironment environment) : PageModel
{
    public PurchaseInvoiceEditPageModel Invoice { get; private set; } = new();

    public int Azione { get; private set; } = FormAzione.Inserimento;

    public bool IsNew => FormAzione.IsInserimento(Azione);

    public string ElectronicInvoiceFolderDefault { get; private set; } = @"C:\";

    public string ReturnUrl { get; private set; } = "/FattureAcquisto";

    public string ReturnLabel { get; private set; } = "Torna alla lista";

    public async Task OnGetAsync(int? id, int? azione, string? returnTo, string? returnUrl, CancellationToken cancellationToken)
    {
        if (string.Equals(returnTo, "menu", StringComparison.OrdinalIgnoreCase))
        {
            ReturnUrl = "/";
            ReturnLabel = "Torna al menu principale";
        }
        else
        {
            ReturnUrl = NormalizeReturnUrl(returnUrl);
        }

        Invoice = id is > 0
            ? await repository.GetEditAsync(id.Value, applicationState.Esercizio, cancellationToken)
            : await repository.GetEditMaskAsync(applicationState.Esercizio, cancellationToken);
        Azione = ResolveAzione(azione, Invoice.Id is not null);
        ElectronicInvoiceFolderDefault = await ElectronicInvoiceFolderDefaultAsync(cancellationToken);
    }

    private static int ResolveAzione(int? azione, bool hasRecord)
    {
        if (azione is > 0)
        {
            return FormAzione.Normalize(azione.Value, FormAzione.ForRecord(hasRecord));
        }

        return FormAzione.ForRecord(hasRecord);
    }

    public async Task<IActionResult> OnGetDuplicateInvoiceAsync(
        string? documentNumber,
        DateOnly? documentDate,
        int? supplierCode,
        int? currentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentNumber) || documentDate is null || supplierCode is null or <= 0)
        {
            return new JsonResult(new { exists = false });
        }

        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT ID,
                   Anno,
                   Codice
            FROM moviva
            WHERE Settore = 10
              AND Ditta = @supplierCode
              AND DataDoc = @documentDate
              AND UPPER(TRIM(COALESCE(NumDoc, ''))) = UPPER(TRIM(@documentNumber))
              AND (@currentId IS NULL OR ID <> @currentId)
            ORDER BY ID DESC
            LIMIT 1;
            """,
            connection);

        command.Parameters.AddWithValue("@supplierCode", supplierCode.Value);
        command.Parameters.Add("@documentDate", MySqlDbType.Date).Value = documentDate.Value.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@documentNumber", documentNumber.Trim());
        command.Parameters.AddWithValue("@currentId", currentId is > 0 ? currentId.Value : DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new JsonResult(new { exists = false });
        }

        return new JsonResult(new
        {
            exists = true,
            id = Convert.ToInt32(reader["ID"]),
            year = Convert.ToInt32(reader["Anno"]),
            code = Convert.ToInt32(reader["Codice"])
        });
    }

    public async Task<IActionResult> OnGetPaymentOptionsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione
            FROM pagamenti
            ORDER BY Descrizione, Codice;
            """,
            connection);

        var rows = new List<object>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = Convert.ToInt32(reader["Codice"]);
            var description = Convert.ToString(reader["Descrizione"]) ?? "";
            rows.Add(new
            {
                code,
                description = $"{code:000} - {description}"
            });
        }

        return new JsonResult(new
        {
            payments = rows
        });
    }

    public async Task<IActionResult> OnGetCalculateDueDatesAsync(
        int paymentCode,
        decimal total,
        string? documentDate,
        CancellationToken cancellationToken)
    {
        if (paymentCode <= 0)
        {
            return new JsonResult(new { success = false, message = "Campo Pagamento obbligatorio." });
        }

        if (total <= 0)
        {
            return new JsonResult(new { success = false, message = "Campo Totale fattura obbligatorio." });
        }

        if (!DateOnly.TryParseExact(
                documentDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var invoiceDate))
        {
            return new JsonResult(new { success = false, message = "Campo Data documento obbligatorio." });
        }

        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT
                COALESCE(NumScadenze, 0) AS NumScadenze,
                COALESCE(Decorrenza, 0) AS Decorrenza,
                COALESCE(PrimoInterv, 0) AS PrimoInterv,
                COALESCE(Intervallo, 0) AS Intervallo,
                COALESCE(TimeOffset, 0) AS TimeOffset,
                COALESCE(SkipAgo, 0) AS SkipAgo,
                COALESCE(SkipDic, 0) AS SkipDic
            FROM pagamenti
            WHERE Codice = @code
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("@code", paymentCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new JsonResult(new { success = false, message = "Codice pagamento non trovato." });
        }

        var dueCount = Convert.ToInt32(reader["NumScadenze"]);
        if (dueCount < 1)
        {
            return new JsonResult(new { success = false, message = "Il codice pagamento non prevede scadenze." });
        }

        var dueRows = CalculateDueRows(
            invoiceDate,
            total,
            dueCount,
            Convert.ToInt32(reader["Decorrenza"]),
            Convert.ToInt32(reader["PrimoInterv"]),
            Convert.ToInt32(reader["Intervallo"]),
            Convert.ToInt32(reader["TimeOffset"]),
            Convert.ToInt32(reader["SkipAgo"]) == 1,
            Convert.ToInt32(reader["SkipDic"]) == 1);

        return new JsonResult(new
        {
            success = true,
            dueRows
        });
    }

    public async Task<IActionResult> OnGetBankOptionsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT Codice, COALESCE(Descrizione, '') AS Descrizione
            FROM conti
            WHERE COALESCE(Ditta, '') = 'B'
            ORDER BY Descrizione, Codice;
            """,
            connection);

        var rows = new List<object>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = Convert.ToInt32(reader["Codice"]);
            var description = Convert.ToString(reader["Descrizione"]) ?? "";
            rows.Add(new
            {
                code,
                description = $"{code:000} - {description}"
            });
        }

        return new JsonResult(new
        {
            banks = rows
        });
    }

    public async Task<IActionResult> OnGetStockLoadBridgeAsync(
        int contraAccountCode,
        CancellationToken cancellationToken)
    {
        if (contraAccountCode <= 0)
        {
            return new JsonResult(new { requiresStockLoad = false });
        }

        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT COALESCE(Carico, 0)
            FROM conti
            WHERE Codice = @code
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("@code", contraAccountCode);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return new JsonResult(new
        {
            requiresStockLoad = value is not null && value != DBNull.Value && Convert.ToInt32(value) != 0
        });
    }

    public async Task<IActionResult> OnPostSaveAsync(
        [FromBody] PurchaseInvoiceSaveCommand invoice,
        string? returnTo,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        invoice.Year = applicationState.Esercizio;
        var validationMessage = ValidateSave(invoice);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return new JsonResult(new
            {
                success = false,
                message = validationMessage
            });
        }

        if (!invoice.ConfirmDueDateMismatch && HasDueDateMismatch(invoice))
        {
            return new JsonResult(new
            {
                success = false,
                requiresDueDateMismatchConfirmation = true,
                message = "Il totale delle scadenze non coincide con il totale fattura.\nVuoi registrare ugualmente?"
            });
        }

        try
        {
            var result = await repository.SaveAsync(invoice, cancellationToken);
            return new JsonResult(new
            {
                success = result.Success,
                requiresOverwriteConfirmation = result.RequiresOverwriteConfirmation,
                message = result.Message,
                id = result.Id,
                year = result.Year,
                code = result.Code,
                redirectUrl = result.Success
                    ? SaveRedirectUrl(returnTo, returnUrl, result.Id)
                    : null
            });
        }
        catch (MySqlException)
        {
            return new JsonResult(new
            {
                success = false,
                message = "Registrazione fattura non riuscita."
            });
        }
    }

    public IActionResult OnPostArchiveElectronicInvoice([FromForm] string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new JsonResult(new { success = true });
        }

        try
        {
            if (!System.IO.File.Exists(path))
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "File XML non trovato per l'archiviazione."
                });
            }

            var sourcePath = Path.GetFullPath(path);
            var sourceFileName = Path.GetFileName(sourcePath);
            if (string.IsNullOrWhiteSpace(sourceFileName))
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Nome file XML non valido."
                });
            }

            Directory.CreateDirectory(servicePaths.FEAcquistiArchivio);
            var sourceDirectory = Path.GetDirectoryName(sourcePath) ?? "";
            var archiveDirectory = Path.GetFullPath(servicePaths.FEAcquistiArchivio);
            var archivePath = Path.GetFullPath(Path.Combine(archiveDirectory, sourceFileName));

            if (string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceDirectory)),
                Path.TrimEndingDirectorySeparator(archiveDirectory),
                StringComparison.OrdinalIgnoreCase))
            {
                return new JsonResult(new { success = true, path = sourcePath });
            }

            System.IO.File.Copy(sourcePath, archivePath, overwrite: true);
            System.IO.File.Delete(sourcePath);

            return new JsonResult(new { success = true, path = archivePath });
        }
        catch (UnauthorizedAccessException)
        {
            return new JsonResult(new
            {
                success = false,
                message = "Permessi insufficienti per archiviare il file XML."
            });
        }
        catch (IOException)
        {
            return new JsonResult(new
            {
                success = false,
                message = "Non e' stato possibile archiviare il file XML."
            });
        }
    }

    private static string SaveRedirectUrl(string? returnTo, string? returnUrl, int? invoiceId)
    {
        if (string.Equals(returnTo, "menu", StringComparison.OrdinalIgnoreCase))
        {
            return "/FattureAcquisto/Edit?returnTo=menu";
        }

        return AddSelectedId(NormalizeReturnUrl(returnUrl), invoiceId);
    }

    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/FattureAcquisto";
        }

        var trimmed = returnUrl.Trim();
        if (!trimmed.StartsWith("/", StringComparison.Ordinal) ||
            trimmed.StartsWith("//", StringComparison.Ordinal) ||
            trimmed.Contains("\\", StringComparison.Ordinal))
        {
            return "/FattureAcquisto";
        }

        return trimmed;
    }

    private static string AddSelectedId(string returnUrl, int? invoiceId)
    {
        if (invoiceId is null)
        {
            return returnUrl;
        }

        var fragmentIndex = returnUrl.IndexOf('#', StringComparison.Ordinal);
        var fragment = fragmentIndex >= 0 ? returnUrl[fragmentIndex..] : string.Empty;
        var pathAndQuery = fragmentIndex >= 0 ? returnUrl[..fragmentIndex] : returnUrl;
        var queryIndex = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            var path = pathAndQuery[..queryIndex];
            var query = pathAndQuery[(queryIndex + 1)..];
            var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(part => !part.StartsWith("selectedId=", StringComparison.OrdinalIgnoreCase));
            pathAndQuery = string.Join("&", parts) is { Length: > 0 } cleanedQuery
                ? $"{path}?{cleanedQuery}"
                : path;
        }

        var separator = pathAndQuery.Contains('?', StringComparison.Ordinal) ? '&' : '?';

        return $"{pathAndQuery}{separator}selectedId={invoiceId.Value}{fragment}";
    }

    public async Task<IActionResult> OnGetElectronicInvoiceFilesAsync(
        string? path,
        string? search,
        CancellationToken cancellationToken)
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        var defaultPath = await ElectronicInvoiceFolderDefaultAsync(cancellationToken);
        var directoryPath = Directory.Exists(defaultPath) ? defaultPath : root;
        var culture = CultureInfo.GetCultureInfo("it-IT");

        try
        {
            var files = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
                .Select(file => new FileInfo(file))
                .Where(file => file.Exists
                    && (file.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
                        || file.Extension.Equals(".p7m", StringComparison.OrdinalIgnoreCase)))
                .Where(file => string.IsNullOrWhiteSpace(search)
                    || file.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(file => new ElectronicInvoiceFileItem(
                    file.Name,
                    file.Extension.TrimStart('.').ToUpperInvariant(),
                    file.LastWriteTime.ToString("dd/MM/yyyy HH:mm", culture),
                    FormatFileSize(file.Length),
                    file.FullName))
                .ToArray();

            return new JsonResult(new
            {
                path = directoryPath,
                count = files.Length,
                files
            });
        }
        catch (UnauthorizedAccessException)
        {
            return new JsonResult(new
            {
                path = directoryPath,
                count = 0,
                files = Array.Empty<ElectronicInvoiceFileItem>(),
                error = "Cartella non accessibile."
            });
        }
        catch (IOException)
        {
            return new JsonResult(new
            {
                path = directoryPath,
                count = 0,
                files = Array.Empty<ElectronicInvoiceFileItem>(),
                error = "Cartella non disponibile."
            });
        }
    }

    public async Task<IActionResult> OnGetElectronicInvoiceFolder(CancellationToken cancellationToken)
    {
        const string script = """
            param([string]$OutputPath)

            Add-Type -AssemblyName System.Windows.Forms
            Add-Type -AssemblyName System.Drawing

            $owner = New-Object System.Windows.Forms.Form
            $owner.Text = 'Micronote'
            $owner.StartPosition = 'CenterScreen'
            $owner.Size = New-Object System.Drawing.Size(1, 1)
            $owner.ShowInTaskbar = $false
            $owner.TopMost = $true
            $owner.Opacity = 0
            $owner.Show()
            $owner.Activate()

            $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
            $dialog.Description = 'Seleziona cartella fatture elettroniche'
            $dialog.ShowNewFolderButton = $false

            if ($dialog.ShowDialog($owner) -eq [System.Windows.Forms.DialogResult]::OK) {
                [System.IO.File]::WriteAllText($OutputPath, $dialog.SelectedPath, [System.Text.Encoding]::UTF8)
            }

            $owner.Close()
            $owner.Dispose()
            """;

        var scriptPath = Path.Combine(Path.GetTempPath(), $"micronote-fe-folder-{Guid.NewGuid():N}.ps1");
        var outputPath = Path.Combine(Path.GetTempPath(), $"micronote-fe-folder-{Guid.NewGuid():N}.txt");
        Process? process = null;

        try
        {
            await System.IO.File.WriteAllTextAsync(scriptPath, script, cancellationToken);

            process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -STA -ExecutionPolicy Bypass -File \"{scriptPath}\" \"{outputPath}\"",
                CreateNoWindow = false,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            var folderPath = System.IO.File.Exists(outputPath)
                ? (await System.IO.File.ReadAllTextAsync(outputPath, cancellationToken)).Trim()
                : string.Empty;

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return new JsonResult(new
                {
                    selected = false
                });
            }

            return new JsonResult(new
            {
                selected = true,
                path = folderPath
            });
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new JsonResult(new
            {
                selected = false,
                error = "Non e' stato possibile aprire la selezione cartella."
            });
        }
        finally
        {
            try
            {
                if (process is not null)
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }

                    process.Dispose();
                }

                if (System.IO.File.Exists(scriptPath))
                {
                    System.IO.File.Delete(scriptPath);
                }

                if (System.IO.File.Exists(outputPath))
                {
                    System.IO.File.Delete(outputPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    public IActionResult OnPostDeleteElectronicInvoiceFile([FromForm] string? path)
    {

        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
        {
            return new JsonResult(new
            {
                success = false,
                message = "File non trovato."
            });
        }

        var extension = Path.GetExtension(path);
        if (!extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".p7m", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonResult(new
            {
                success = false,
                message = "Tipo file non eliminabile."
            });
        }

        try
        {
            System.IO.File.Delete(path);
            return new JsonResult(new
            {
                success = true
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new JsonResult(new
            {
                success = false,
                message = "Non e' stato possibile eliminare il file."
            });
        }
    }

    public IActionResult OnGetElectronicInvoicePreview(string? path, string? fileName)
    {
        servicePaths.EnsureCreated();
        var requestedFileName = !string.IsNullOrWhiteSpace(fileName) ? fileName : path;
        var safeFileName = Path.GetFileName(requestedFileName?.Trim() ?? string.Empty);
        path = string.IsNullOrWhiteSpace(safeFileName)
            ? null
            : Path.Combine(servicePaths.FEAcquistiTransito, safeFileName);


        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
        {
            return new JsonResult(new
            {
                success = false,
                message = "File non trovato."
            });
        }

        try
        {
            var document = LoadElectronicInvoiceDocument(path);
            var supplier = SubjectName(FirstDescendant(document, "CedentePrestatore"));
            var customer = SubjectName(FirstDescendant(document, "CessionarioCommittente"));
            var documentData = FirstDescendant(document, "DatiGeneraliDocumento");
            var number = ChildValue(documentData, "Numero");
            var date = FormatXmlDate(ChildValue(documentData, "Data"));
            var amount = FormatXmlAmount(ChildValue(documentData, "ImportoTotaleDocumento"));

            return new JsonResult(new
            {
                success = true,
                supplier,
                customer,
                number,
                date,
                amount
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException or InvalidDataException)
        {
            return new JsonResult(new
            {
                success = false,
                message = "File XML/P7M non leggibile."
            });
        }
    }

    public async Task<IActionResult> OnGetElectronicInvoiceImportAsync(string? path, string? fileName, CancellationToken cancellationToken)
    {
        servicePaths.EnsureCreated();
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
        {
            var requestedFileName = !string.IsNullOrWhiteSpace(fileName) ? fileName : path;
            var safeFileName = Path.GetFileName(requestedFileName?.Trim() ?? string.Empty);
            path = string.IsNullOrWhiteSpace(safeFileName)
                ? null
                : Path.Combine(servicePaths.FEAcquistiTransito, safeFileName);
        }

        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
        {
            return new JsonResult(new
            {
                success = false,
                message = "File non trovato."
            });
        }

        try
        {
            var document = LoadElectronicInvoiceDocument(path);
            var customer = FirstDescendant(document, "CessionarioCommittente");
            var customerData = FirstDescendant(customer, "DatiAnagrafici");
            var customerVat = NormalizeFiscalCode(ChildValue(FirstDescendant(customerData, "IdFiscaleIVA"), "IdCodice"));
            var customerFiscalCode = NormalizeFiscalCode(ChildValue(customerData, "CodiceFiscale"));
            var companyFiscalData = await CompanyFiscalDataAsync(cancellationToken);
            var companyFiscalError = ValidateCompanyFiscalData(companyFiscalData);
            if (!string.IsNullOrWhiteSpace(companyFiscalError))
            {
                return new JsonResult(new
                {
                    success = false,
                    message = companyFiscalError
                });
            }

            var companyMatches = false;
            if (!string.IsNullOrWhiteSpace(companyFiscalData.VatNumber)
                && !string.IsNullOrWhiteSpace(customerVat)
                && string.Equals(companyFiscalData.VatNumber, customerVat, StringComparison.OrdinalIgnoreCase))
            {
                companyMatches = true;
            }

            if (!string.IsNullOrWhiteSpace(companyFiscalData.FiscalCode)
                && !string.IsNullOrWhiteSpace(customerFiscalCode)
                && string.Equals(companyFiscalData.FiscalCode, customerFiscalCode, StringComparison.OrdinalIgnoreCase))
            {
                companyMatches = true;
            }

            if (!companyMatches)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "La fattura elettronica non risulta intestata all'azienda corrente."
                });
            }

            var supplier = FirstDescendant(document, "CedentePrestatore");
            var supplierData = FirstDescendant(supplier, "DatiAnagrafici");
            var supplierVat = ChildValue(FirstDescendant(supplierData, "IdFiscaleIVA"), "IdCodice");
            var supplierFiscalCode = ChildValue(supplierData, "CodiceFiscale");
            var supplierName = SubjectName(supplier);
            var supplierOffice = FirstDescendant(supplier, "Sede");
            var supplierContacts = FirstDescendant(supplier, "Contatti");
            var supplierAddress = JoinNonEmpty(" ",
                ChildValue(supplierOffice, "Indirizzo"),
                ChildValue(supplierOffice, "NumeroCivico"));
            var supplierPostalCode = ChildValue(supplierOffice, "CAP");
            var supplierCityRaw = ChildValue(supplierOffice, "Comune");
            var supplierProvinceRaw = ChildValue(supplierOffice, "Provincia");
            var (supplierCity, supplierProvince) = SplitCityAndProvince(supplierCityRaw, supplierProvinceRaw);
            var supplierCountry = ChildValue(supplierOffice, "Nazione");
            var supplierPhone = ChildValue(supplierContacts, "Telefono");
            var supplierEmail = ChildValue(supplierContacts, "Email");
            var supplierCertifiedEmail = ChildValue(supplierContacts, "PECDestinatario");
            var matchedSupplier = await FindSupplierByFiscalDataAsync(supplierVat, supplierFiscalCode, cancellationToken);
            if (matchedSupplier is null)
            {
                return new JsonResult(new
                {
                    success = false,
                    reason = "supplierMissing",
                    message = "Fornitore non presente in anagrafica.",
                    supplier = new
                    {
                        name = supplierName,
                        vat = supplierVat,
                        fiscalCode = supplierFiscalCode,
                        address = supplierAddress,
                        postalCode = supplierPostalCode,
                        city = supplierCity,
                        province = supplierProvince,
                        country = supplierCountry,
                        phone = supplierPhone,
                        email = supplierEmail,
                        certifiedEmail = supplierCertifiedEmail
                    }
                });
            }

            var documentData = FirstDescendant(document, "DatiGeneraliDocumento");
            var documentType = ChildValue(documentData, "TipoDocumento");
            var documentNumber = ChildValue(documentData, "Numero");
            var documentDate = FormatXmlDateForInput(ChildValue(documentData, "Data"));
            if (!DateOnly.TryParse(documentDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDocumentDate))
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Data documento mancante o non valida nella fattura elettronica."
                });
            }

            if (parsedDocumentDate.Year != applicationState.Esercizio)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = $"La data documento non appartiene all'esercizio contabile in linea ({applicationState.Esercizio})."
                });
            }

            var total = ParseXmlDecimal(ChildValue(documentData, "ImportoTotaleDocumento"));

            var vatRows = document.Descendants()
                .Where(element => element.Name.LocalName.Equals("DatiRiepilogo", StringComparison.Ordinal))
                .Select(element => new
                {
                    Rate = ParseXmlDecimal(ChildValue(element, "AliquotaIVA")),
                    Taxable = ParseXmlDecimal(ChildValue(element, "ImponibileImporto")),
                    Tax = ParseXmlDecimal(ChildValue(element, "Imposta"))
                })
                .Where(row => row.Rate is not null || row.Taxable is not null || row.Tax is not null)
                .GroupBy(row => row.Rate ?? 0)
                .OrderBy(group => group.Key)
                .Select(group => new
                {
                    rate = FormatItalianNumber(group.Key),
                    taxable = FormatItalianAmount(group.Sum(row => row.Taxable ?? 0)),
                    tax = FormatItalianAmount(group.Sum(row => row.Tax ?? 0))
                })
                .Take(4)
                .ToArray();

            var taxableTotal = vatRows
                .Select(row => ParseItalianDecimal(row.taxable))
                .Sum();
            var vatTotal = vatRows
                .Select(row => ParseItalianDecimal(row.tax))
                .Sum();

            var dueRows = document.Descendants()
                .Where(element => element.Name.LocalName.Equals("DettaglioPagamento", StringComparison.Ordinal))
                .Select((element, index) => new
                {
                    number = (index + 1).ToString(CultureInfo.InvariantCulture),
                    amount = FormatItalianAmount(ParseXmlDecimal(ChildValue(element, "ImportoPagamento"))),
                    date = FormatXmlDateForInput(ChildValue(element, "DataScadenzaPagamento"))
                })
                .Where(row => !string.IsNullOrWhiteSpace(row.amount) || !string.IsNullOrWhiteSpace(row.date))
                .Take(6)
                .ToArray();
            var electronicPaymentMode = document.Descendants()
                .Where(element => element.Name.LocalName.Equals("DettaglioPagamento", StringComparison.Ordinal))
                .Select(element => ChildValue(element, "ModalitaPagamento"))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
            var paymentCode = await FindPaymentCodeByElectronicModeAsync(electronicPaymentMode, cancellationToken);

            return new JsonResult(new
            {
                success = true,
                fileName = Path.GetFileName(path),
                fullPath = path,
                documentType,
                documentNumber,
                documentDate,
                total = FormatItalianAmount(total),
                inserted = FormatItalianAmount(total),
                difference = FormatItalianAmount((total ?? 0) - (taxableTotal + vatTotal)),
                supplier = new
                {
                    code = matchedSupplier?.Code,
                    codeDisplay = matchedSupplier is null ? "" : matchedSupplier.Code.ToString("00000", CultureInfo.InvariantCulture),
                    name = matchedSupplier?.Name ?? supplierName,
                    found = matchedSupplier is not null,
                    vat = supplierVat,
                    fiscalCode = supplierFiscalCode,
                    accountCode = matchedSupplier?.AccountCode,
                    storeCode = matchedSupplier?.StoreCode
                },
                vatRows,
                vatTotals = new
                {
                    taxable = FormatItalianAmount(taxableTotal),
                    tax = FormatItalianAmount(vatTotal)
                },
                paymentCode,
                dueRows
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException or InvalidDataException)
        {
            return new JsonResult(new
            {
                success = false,
                message = "File XML/P7M non leggibile."
            });
        }
    }

    public async Task<IActionResult> OnGetElectronicInvoiceRawAsync(
        string? path,
        string? fileName,
        string? source,
        CancellationToken cancellationToken)
    {
        var requestedFileName = !string.IsNullOrWhiteSpace(fileName) ? fileName : path;
        var safeFileName = Path.GetFileName(requestedFileName?.Trim() ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(safeFileName))
        {
            var electronicInvoiceFolder = string.Equals(source, "archive", StringComparison.OrdinalIgnoreCase)
                ? servicePaths.FEAcquistiArchivio
                : await ElectronicInvoiceFolderDefaultAsync(cancellationToken);
            path = Path.Combine(electronicInvoiceFolder, safeFileName);
        }


        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
        {
            return Content("File non trovato.", "text/plain", Encoding.UTF8);
        }

        try
        {
            var document = LoadElectronicInvoiceDocument(path);
            var stylesheetPath = Path.Combine(environment.WebRootPath, "xsl", "fattura_elettronica.xsl");
            if (!System.IO.File.Exists(stylesheetPath))
            {
                return Content("Foglio XSL non trovato.", "text/plain", Encoding.UTF8);
            }

            var transform = new XslCompiledTransform();
            transform.Load(stylesheetPath);

            using var xmlReader = document.CreateReader();
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            transform.Transform(xmlReader, null, writer);

            return Content(writer.ToString(), "text/html; charset=utf-8", Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException or InvalidDataException)
        {
            return Content("File XML/P7M non leggibile.", "text/plain", Encoding.UTF8);
        }
        catch (XsltException)
        {
            return Content("Trasformazione XSL non riuscita.", "text/plain", Encoding.UTF8);
        }
    }

    private string ValidateSave(PurchaseInvoiceSaveCommand invoice)
    {
        if (invoice.CauseCode <= 0)
        {
            return "Campo Tipo documento obbligatorio.";
        }

        if (string.IsNullOrWhiteSpace(invoice.DocumentNumber))
        {
            return "Campo Numero documento obbligatorio.";
        }

        if (invoice.DocumentDate is null)
        {
            return "Campo Data documento obbligatorio.";
        }

        if (invoice.SupplierCode <= 0)
        {
            return "Campo Fornitore obbligatorio.";
        }

        if (invoice.ContraAccountCode <= 0)
        {
            return "Campo Contropartita obbligatorio.";
        }

        if (invoice.Total <= 0)
        {
            return "Campo Totale fattura obbligatorio.";
        }

        if (invoice.VatRows.Count == 0
            || !invoice.VatRows.Any(row => row.Taxable != 0 || row.Tax != 0))
        {
            return "Inserire almeno una riga di dettaglio imponibile/iva.";
        }

        if (invoice.DocumentDate.Value.Year != applicationState.Esercizio)
        {
            return $"La data documento deve appartenere all'esercizio contabile in linea ({applicationState.Esercizio}).";
        }

        var insertedTotal = invoice.VatRows.Sum(row => row.Taxable + row.Tax);
        if (decimal.Round(insertedTotal - invoice.Total, 2, MidpointRounding.AwayFromZero) != 0)
        {
            return "Quadratura importi errata.";
        }

        if (invoice.VatRows.Any(row => row.Rate < 0 || row.Rate > 100))
        {
            return "Aliquota iva non valida.";
        }

        var dueRows = invoice.DueRows
            .Where(row => row.Amount != 0 || row.Date is not null)
            .ToArray();
        foreach (var row in dueRows)
        {
            if (row.Amount == 0)
            {
                return "Campo Importo scadenza obbligatorio.";
            }

            if (row.Date is null)
            {
                return "Campo Scadenza obbligatorio.";
            }
        }

        if (dueRows.Length > 0 && invoice.PaymentCode <= 0)
        {
            return "Campo Pagamento obbligatorio.";
        }

        return "";
    }

    private static bool HasDueDateMismatch(PurchaseInvoiceSaveCommand invoice)
    {
        var dueRows = invoice.DueRows
            .Where(row => row.Amount != 0 || row.Date is not null)
            .ToArray();
        var dueTotal = decimal.Round(dueRows.Sum(row => row.Amount), 2, MidpointRounding.AwayFromZero);
        var invoiceTotal = decimal.Round(invoice.Total, 2, MidpointRounding.AwayFromZero);
        return dueTotal != 0
            && decimal.Round(dueTotal - invoiceTotal, 2, MidpointRounding.AwayFromZero) != 0;
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 KB";
        }

        var kilobytes = Math.Max(1, (int)Math.Round(bytes / 1024m, MidpointRounding.AwayFromZero));
        return $"{kilobytes:N0} KB";
    }

    private Task<string> ElectronicInvoiceFolderDefaultAsync(CancellationToken cancellationToken)
    {
        servicePaths.EnsureCreated();
        return Task.FromResult(servicePaths.FEAcquistiTransito);
    }


    private async Task<SupplierMatch?> FindSupplierByFiscalDataAsync(
        string supplierVat,
        string supplierFiscalCode,
        CancellationToken cancellationToken)
    {
        var vat = NormalizeFiscalCode(supplierVat);
        var fiscalCode = NormalizeFiscalCode(supplierFiscalCode);
        if (string.IsNullOrWhiteSpace(vat) && string.IsNullOrWhiteSpace(fiscalCode))
        {
            return null;
        }

        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT Codice,
                   COALESCE(Nome, '') AS Nome,
                   Contropartita,
                   PuntoV
            FROM fornitori
            WHERE (@vat <> '' AND UPPER(REPLACE(REPLACE(REPLACE(COALESCE(Piva, ''), ' ', ''), '-', ''), '.', '')) = @vat)
               OR (@fiscalCode <> '' AND UPPER(REPLACE(REPLACE(REPLACE(COALESCE(Codfi, ''), ' ', ''), '-', ''), '.', '')) = @fiscalCode)
            ORDER BY
                CASE
                    WHEN @vat <> '' AND UPPER(REPLACE(REPLACE(REPLACE(COALESCE(Piva, ''), ' ', ''), '-', ''), '.', '')) = @vat THEN 0
                    ELSE 1
                END,
                Nome,
                Codice
            LIMIT 1;
            """,
            connection);

        command.Parameters.AddWithValue("@vat", vat);
        command.Parameters.AddWithValue("@fiscalCode", fiscalCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SupplierMatch(
            Convert.ToInt32(reader["Codice"]),
            Convert.ToString(reader["Nome"]) ?? "",
            reader["Contropartita"] == DBNull.Value ? null : Convert.ToInt32(reader["Contropartita"]),
            reader["PuntoV"] == DBNull.Value ? null : Convert.ToInt32(reader["PuntoV"]));
    }

    private async Task<int?> FindPaymentCodeByElectronicModeAsync(
        string electronicPaymentMode,
        CancellationToken cancellationToken)
    {
        var mode = (electronicPaymentMode ?? "").Trim();
        if (string.IsNullOrWhiteSpace(mode))
        {
            return null;
        }

        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT Codice
            FROM pagamenti
            WHERE UPPER(TRIM(COALESCE(Modalita, ''))) = UPPER(TRIM(@mode))
            ORDER BY Codice
            LIMIT 1;
            """,
            connection);

        command.Parameters.AddWithValue("@mode", mode);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value == DBNull.Value ? null : Convert.ToInt32(value);
    }

    private async Task<CompanyFiscalData> CompanyFiscalDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await db.OpenConnectionAsync(cancellationToken);
            await using var command = new MySqlCommand(
                """
                SELECT Chiave, COALESCE(Valore, '') AS Valore
                FROM Opzioni
                WHERE Chiave IN ('CodiceFiscale', 'PartitaIva');
                """,
                connection);

            var fiscalCode = "";
            var vatNumber = "";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var key = reader.GetString("Chiave");
                var value = NormalizeFiscalCode(reader.GetString("Valore"));
                if (key.Equals("CodiceFiscale", StringComparison.OrdinalIgnoreCase))
                {
                    fiscalCode = value;
                }
                else if (key.Equals("PartitaIva", StringComparison.OrdinalIgnoreCase))
                {
                    vatNumber = value;
                }
            }

            return new CompanyFiscalData(fiscalCode, vatNumber);
        }
        catch (MySqlException ex) when (ex.Number is 1054 or 1146)
        {
            return new CompanyFiscalData("", "");
        }
    }

    private static string ValidateCompanyFiscalData(CompanyFiscalData companyFiscalData)
    {
        if (string.IsNullOrWhiteSpace(companyFiscalData.FiscalCode))
        {
            return "Codice fiscale azienda mancante. Registrarlo in Impostazioni.";
        }

        if (companyFiscalData.FiscalCode.Length is not (11 or 16))
        {
            return "Codice fiscale azienda non valido. Verificare le Impostazioni.";
        }

        if (string.IsNullOrWhiteSpace(companyFiscalData.VatNumber))
        {
            return "Partita IVA azienda mancante. Registrarla in Impostazioni.";
        }

        if (companyFiscalData.VatNumber.Length != 11
            || companyFiscalData.VatNumber.Any(character => !char.IsDigit(character)))
        {
            return "Partita IVA azienda non valida. Verificare le Impostazioni.";
        }

        return "";
    }

    private static string NormalizeFiscalCode(string value) =>
        Regex.Replace(value ?? "", @"[\s\-.]", "").ToUpperInvariant();

    private static string JoinNonEmpty(string separator, params string[] values) =>
        string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));

    private static (string City, string Province) SplitCityAndProvince(string city, string province)
    {
        var normalizedCity = city?.Trim() ?? "";
        var normalizedProvince = province?.Trim().ToUpperInvariant() ?? "";
        var match = Regex.Match(normalizedCity, @"^(?<city>.+?)\s*\((?<province>[A-Za-z]{2})\)\s*$");
        if (!match.Success)
        {
            return (normalizedCity, normalizedProvince);
        }

        var extractedProvince = match.Groups["province"].Value.Trim().ToUpperInvariant();
        return (
            match.Groups["city"].Value.Trim(),
            string.IsNullOrWhiteSpace(normalizedProvince) ? extractedProvince : normalizedProvince);
    }

    private static XElement? FirstDescendant(XDocument document, string localName) =>
        document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName.Equals(localName, StringComparison.Ordinal));

    private static XDocument LoadElectronicInvoiceDocument(string path)
    {
        if (!Path.GetExtension(path).Equals(".p7m", StringComparison.OrdinalIgnoreCase))
        {
            return XDocument.Load(path);
        }

        var xmlBytes = ExtractXmlFromP7m(path);
        using var stream = new MemoryStream(xmlBytes);
        return XDocument.Load(stream);
    }

    private static byte[] ExtractXmlFromP7m(string path)
    {
        var bytes = System.IO.File.ReadAllBytes(path);
        foreach (var candidate in ExtractBerOctetStringCandidates(bytes))
        {
            if (TryExtractXmlFragment(candidate, out var xmlBytes))
            {
                return xmlBytes;
            }
        }

        if (TryExtractXmlFragment(bytes, out var rawXmlBytes))
        {
            return rawXmlBytes;
        }

        throw new InvalidDataException("Contenuto XML non trovato nel file P7M.");
    }

    private static bool TryExtractXmlFragment(byte[] bytes, out byte[] xmlBytes)
    {
        var content = Encoding.Latin1.GetString(bytes);
        var start = content.IndexOf("<?xml", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            var rootMatch = Regex.Match(
                content,
                @"<([A-Za-z0-9_]+:)?FatturaElettronica\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            start = rootMatch.Success ? rootMatch.Index : -1;
        }

        if (start < 0)
        {
            xmlBytes = [];
            return false;
        }

        var closeMatches = Regex.Matches(
            content[start..],
            @"</([A-Za-z0-9_]+:)?FatturaElettronica>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (closeMatches.Count == 0)
        {
            xmlBytes = [];
            return false;
        }

        var close = closeMatches[^1];
        var end = start + close.Index + close.Length;
        xmlBytes = bytes[start..end];
        return true;
    }

    private static IEnumerable<byte[]> ExtractBerOctetStringCandidates(byte[] bytes)
    {
        var position = 0;
        while (position < bytes.Length)
        {
            if (!TryReadBerElement(bytes, position, bytes.Length, out var element))
            {
                yield break;
            }

            foreach (var candidate in ExtractBerOctetStringCandidates(bytes, element))
            {
                yield return candidate;
            }

            position = element.End;
        }
    }

    private static IEnumerable<byte[]> ExtractBerOctetStringCandidates(byte[] bytes, BerElement element)
    {
        if (element.TagClass == 0 && element.TagNumber == 4)
        {
            if (element.Constructed)
            {
                var chunks = new List<byte[]>();
                CollectBerOctetStringChunks(bytes, element.ContentStart, element.ContentEnd, chunks);
                if (chunks.Count > 0)
                {
                    yield return chunks.SelectMany(chunk => chunk).ToArray();
                }
            }
            else
            {
                yield return bytes[element.ContentStart..element.ContentEnd];
            }
        }

        if (!element.Constructed)
        {
            yield break;
        }

        var position = element.ContentStart;
        while (position < element.ContentEnd)
        {
            if (!TryReadBerElement(bytes, position, element.ContentEnd, out var child))
            {
                yield break;
            }

            foreach (var candidate in ExtractBerOctetStringCandidates(bytes, child))
            {
                yield return candidate;
            }

            position = child.End;
        }
    }

    private static void CollectBerOctetStringChunks(byte[] bytes, int start, int end, List<byte[]> chunks)
    {
        var position = start;
        while (position < end)
        {
            if (!TryReadBerElement(bytes, position, end, out var child))
            {
                return;
            }

            if (child.TagClass == 0 && child.TagNumber == 4)
            {
                if (child.Constructed)
                {
                    CollectBerOctetStringChunks(bytes, child.ContentStart, child.ContentEnd, chunks);
                }
                else
                {
                    chunks.Add(bytes[child.ContentStart..child.ContentEnd]);
                }
            }

            position = child.End;
        }
    }

    private static bool TryReadBerElement(byte[] bytes, int start, int limit, out BerElement element)
    {
        element = default;
        if (start >= limit)
        {
            return false;
        }

        var position = start;
        var first = bytes[position++];
        if (first == 0 && position < limit && bytes[position] == 0)
        {
            return false;
        }

        var tagClass = (first & 0b1100_0000) >> 6;
        var constructed = (first & 0b0010_0000) != 0;
        var tagNumber = first & 0b0001_1111;
        if (tagNumber == 0b0001_1111)
        {
            tagNumber = 0;
            byte tagByte;
            do
            {
                if (position >= limit)
                {
                    return false;
                }

                tagByte = bytes[position++];
                tagNumber = (tagNumber << 7) | (tagByte & 0x7F);
            }
            while ((tagByte & 0x80) != 0);
        }

        if (position >= limit)
        {
            return false;
        }

        var lengthByte = bytes[position++];
        var contentStart = position;
        int contentEnd;
        int elementEnd;
        if (lengthByte == 0x80)
        {
            var scan = contentStart;
            while (scan + 1 < limit && !(bytes[scan] == 0 && bytes[scan + 1] == 0))
            {
                if (!TryReadBerElement(bytes, scan, limit, out var child))
                {
                    return false;
                }

                scan = child.End;
            }

            if (scan + 1 >= limit)
            {
                return false;
            }

            contentEnd = scan;
            elementEnd = scan + 2;
        }
        else
        {
            int length;
            if ((lengthByte & 0x80) == 0)
            {
                length = lengthByte;
            }
            else
            {
                var lengthBytes = lengthByte & 0x7F;
                if (lengthBytes == 0 || lengthBytes > 4 || position + lengthBytes > limit)
                {
                    return false;
                }

                length = 0;
                for (var index = 0; index < lengthBytes; index++)
                {
                    length = (length << 8) | bytes[position++];
                }

                contentStart = position;
            }

            contentEnd = contentStart + length;
            elementEnd = contentEnd;
            if (contentEnd > limit)
            {
                return false;
            }
        }

        element = new BerElement(tagClass, tagNumber, constructed, contentStart, contentEnd, elementEnd);
        return true;
    }

    private static XElement? FirstDescendant(XElement? element, string localName) =>
        element?.Descendants()
            .FirstOrDefault(child => child.Name.LocalName.Equals(localName, StringComparison.Ordinal));

    private static string ChildValue(XElement? element, string localName) =>
        element?.Elements()
            .FirstOrDefault(child => child.Name.LocalName.Equals(localName, StringComparison.Ordinal))
            ?.Value
            .Trim()
        ?? "";

    private static string SubjectName(XElement? subject)
    {
        var registry = FirstDescendant(subject, "Anagrafica");
        var companyName = ChildValue(registry, "Denominazione");
        if (!string.IsNullOrWhiteSpace(companyName))
        {
            return companyName;
        }

        var firstName = ChildValue(registry, "Nome");
        var lastName = ChildValue(registry, "Cognome");
        return string.Join(" ", new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string FormatXmlDate(string value)
    {
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date.ToString("dd-MM-yyyy", CultureInfo.GetCultureInfo("it-IT"));
        }

        return value;
    }

    private static string FormatXmlAmount(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return amount.ToString("#,##0.00", CultureInfo.GetCultureInfo("it-IT"));
        }

        return value;
    }

    private static string FormatXmlDateForInput(string value)
    {
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return "";
    }

    private static decimal? ParseXmlDecimal(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return amount;
        }

        return null;
    }

    private static decimal ParseItalianDecimal(string value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("it-IT"), out var amount)
            ? amount
            : 0;
    }

    private static string FormatItalianAmount(decimal? value) =>
        value is null || value == 0
            ? ""
            : value.Value.ToString("#,##0.00", CultureInfo.GetCultureInfo("it-IT"));

    private static object[] CalculateDueRows(
        DateOnly invoiceDate,
        decimal total,
        int dueCount,
        int dueType,
        int firstInterval,
        int interval,
        int timeOffset,
        bool skipAugust,
        bool skipDecember)
    {
        var rows = new List<object>();
        var startDate = invoiceDate;
        var dueDate = startDate;
        var subtotal = 0m;
        var installment = decimal.Round(total / dueCount, 2, MidpointRounding.AwayFromZero);

        for (var number = 1; number <= dueCount; number++)
        {
            dueDate = number == 1
                ? startDate.AddMonths(firstInterval).AddDays(timeOffset)
                : dueDate.AddMonths(interval);

            if (dueType == 3)
            {
                dueDate = EndOfMonth(dueDate);
            }

            if (skipAugust && dueDate.Month == 8)
            {
                dueDate = dueDate.AddMonths(1);
            }

            if (skipDecember && dueDate.Month == 12)
            {
                dueDate = dueDate.AddMonths(1);
            }

            var amount = number == dueCount
                ? total - subtotal
                : installment;
            amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            subtotal += amount;

            rows.Add(new
            {
                number,
                amount = FormatItalianAmount(amount),
                date = dueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                paid = false
            });
        }

        return rows.ToArray();
    }

    private static DateOnly EndOfMonth(DateOnly date) =>
        new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

    private static string FormatItalianNumber(decimal? value) =>
        value is null
            ? ""
            : value.Value.ToString("#,##0.00", CultureInfo.GetCultureInfo("it-IT"));

    private readonly record struct BerElement(
        int TagClass,
        int TagNumber,
        bool Constructed,
        int ContentStart,
        int ContentEnd,
        int End);

    private sealed record ElectronicInvoiceFileItem(
        string Name,
        string Type,
        string LastModified,
        string Size,
        string FullPath);

    private sealed record SupplierMatch(int Code, string Name, int? AccountCode, int? StoreCode);

    private sealed record CompanyFiscalData(string FiscalCode, string VatNumber);
}






