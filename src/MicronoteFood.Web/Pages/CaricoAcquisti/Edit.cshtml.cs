using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Pages.CaricoAcquisti;

public sealed class EditModel(
    PurchaseInvoiceRepository purchaseInvoiceRepository,
    StockLoadRepository stockLoadRepository,
    MicronoteDb db,
    ApplicationState applicationState,
    MicronoteServicePaths servicePaths,
    IWebHostEnvironment environment) : PageModel
{
    public PurchaseInvoiceEditPageModel Mask { get; private set; } = new();

    public StockLoadEditDocument Document { get; private set; } = new();

    public bool IsEditMode => Document.Id > 0;

    public string ReturnUrl { get; private set; } = "/";

    public string ReturnLabel { get; private set; } = "Torna al menu principale";

    public int Azione { get; private set; } = FormAzione.Inserimento;

    public bool IsReadonly => FormAzione.IsReadonly(Azione);

    public bool IsModal => FormAzione.IsModale(Azione);

    public string ElectronicInvoiceFolderDefault { get; private set; } = @"C:\";

    [BindProperty]
    public string StockLoadPayload { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(
        int? id,
        int? azione,
        string? returnTo,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        Azione = FormAzione.Normalize(
            azione ?? FormAzione.ForRecord(id.GetValueOrDefault() > 0),
            FormAzione.ForRecord(id.GetValueOrDefault() > 0));

        if (string.Equals(returnTo, "list", StringComparison.OrdinalIgnoreCase))
        {
            ReturnUrl = NormalizeReturnUrl(returnUrl) ?? "/CaricoAcquisti/Index";
            ReturnLabel = "Torna alla lista documenti di carico";
        }
        else if (string.Equals(returnTo, "stockMovement", StringComparison.OrdinalIgnoreCase))
        {
            ReturnUrl = NormalizeReturnUrl(returnUrl) ?? "/MovimentiMagazzino/Index";
            ReturnLabel = "Torna alla lista movimenti";
        }
        else if (string.Equals(returnTo, "purchaseInvoice", StringComparison.OrdinalIgnoreCase))
        {
            ReturnUrl = "#";
            ReturnLabel = "Torna alla fattura di acquisto";
        }

        ElectronicInvoiceFolderDefault = await ElectronicInvoiceFolderDefaultAsync(cancellationToken);

        Mask = await purchaseInvoiceRepository.GetEditMaskAsync(
            applicationState.Esercizio,
            cancellationToken);

        var documentId = id.GetValueOrDefault();
        if (documentId <= 0)
        {
            return Page();
        }

        var document = await stockLoadRepository.GetEditAsync(documentId, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        Document = document;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        StockLoadSaveCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<StockLoadSaveCommand>(
                StockLoadPayload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return new JsonResult(new { success = false, message = "Dati di registrazione non leggibili." });
        }

        if (command is null)
        {
            return new JsonResult(new { success = false, message = "Dati di registrazione mancanti." });
        }

        command.Year = command.Year == 0 ? applicationState.Esercizio : command.Year;
        command.CauseCode = command.CauseCode == 0 ? 10 : command.CauseCode;
        var validationError = ValidateOverwriteCheckCommand(command);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return new JsonResult(new { success = false, message = validationError });
        }

        var missingArticle = await FirstMissingArticleAsync(command, cancellationToken);
        if (!string.IsNullOrWhiteSpace(missingArticle))
        {
            return new JsonResult(new
            {
                success = false,
                message = $"Articolo non presente in anagrafica: {missingArticle}."
            });
        }

        var result = await stockLoadRepository.SaveAsync(command, cancellationToken);
        if (!result.Success)
        {
            return new JsonResult(new
            {
                success = false,
                message = result.Message,
                requiresOverwrite = result.RequiresOverwrite,
                id = result.Id,
                year = result.Year,
                code = result.Code
            });
        }

        var archiveCopy = await TryCopyElectronicInvoiceToArchiveAsync(command, cancellationToken);

        return new JsonResult(new
        {
            success = true,
            id = result.Id,
            year = result.Year,
            code = result.Code,
            overwritten = result.Overwritten,
            goods = result.Goods,
            vat = result.Vat,
            total = result.Total,
            electronicInvoiceArchived = archiveCopy.Copied,
            electronicInvoiceArchiveWarning = archiveCopy.Warning,
            editUrl = Url.Page("/CaricoAcquisti/Edit", new
            {
                id = result.Id,
                returnTo = "list",
                returnUrl = "/CaricoAcquisti/Index"
            })
        });
    }

    private async Task<ElectronicInvoiceArchiveCopyResult> TryCopyElectronicInvoiceToArchiveAsync(
        StockLoadSaveCommand command,
        CancellationToken cancellationToken)
    {
        var sourcePath = command.ElectronicInvoicePath.Trim();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return ElectronicInvoiceArchiveCopyResult.Empty;
        }

        try
        {
            var fullSourcePath = Path.GetFullPath(sourcePath);
            if (!System.IO.File.Exists(fullSourcePath))
            {
                return new(false, "File XML non trovato nella cartella di origine.");
            }

            var fileName = Path.GetFileName(fullSourcePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return ElectronicInvoiceArchiveCopyResult.Empty;
            }

            var archiveDirectory = Path.GetFullPath(await ElectronicInvoiceArchiveFolderAsync(cancellationToken));
            Directory.CreateDirectory(archiveDirectory);

            var destinationPath = Path.Combine(archiveDirectory, fileName);
            if (string.Equals(fullSourcePath, Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
            {
                return ElectronicInvoiceArchiveCopyResult.Empty;
            }

            System.IO.File.Copy(fullSourcePath, destinationPath, overwrite: true);
            return new(true, "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new(false, "Carico salvato, ma non e' stato possibile copiare l'XML nell'archivio finale.");
        }
    }

    public async Task<IActionResult> OnPostOverwriteCheckAsync(CancellationToken cancellationToken)
    {
        StockLoadSaveCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<StockLoadSaveCommand>(
                StockLoadPayload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return new JsonResult(new { success = false, message = "Dati di registrazione non leggibili." });
        }

        if (command is null)
        {
            return new JsonResult(new { success = false, message = "Dati di registrazione mancanti." });
        }

        command.Year = command.Year == 0 ? applicationState.Esercizio : command.Year;
        command.CauseCode = command.CauseCode == 0 ? 10 : command.CauseCode;
        var validationError = ValidateOverwriteCheckCommand(command);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return new JsonResult(new { success = false, message = validationError });
        }

        var result = await stockLoadRepository.CheckOverwriteAsync(command, cancellationToken);
        return new JsonResult(new
        {
            success = result.Success,
            message = result.Message,
            requiresOverwrite = result.RequiresOverwrite,
            id = result.Id,
            year = result.Year,
            code = result.Code
        });
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
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -STA -ExecutionPolicy Bypass -File \"{scriptPath}\" \"{outputPath}\"",
                    CreateNoWindow = false,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            var folderPath = System.IO.File.Exists(outputPath)
                ? (await System.IO.File.ReadAllTextAsync(outputPath, cancellationToken)).Trim()
                : string.Empty;

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return new JsonResult(new { selected = false });
            }

            return new JsonResult(new { selected = true, path = folderPath });
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
            return new JsonResult(new { success = false, message = "File non trovato." });
        }

        var extension = Path.GetExtension(path);
        if (!extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".p7m", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonResult(new { success = false, message = "Tipo file non eliminabile." });
        }

        try
        {
            System.IO.File.Delete(path);
            return new JsonResult(new { success = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new JsonResult(new { success = false, message = "Non e' stato possibile eliminare il file." });
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
            return new JsonResult(new { success = false, message = "File non trovato." });
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
            return new JsonResult(new { success = false, message = "File XML/P7M non leggibile." });
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
            return new JsonResult(new { success = false, message = "File non trovato." });
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
                return new JsonResult(new { success = false, message = companyFiscalError });
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
                        fiscalCode = supplierFiscalCode
                    }
                });
            }

            var documentData = FirstDescendant(document, "DatiGeneraliDocumento");
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

            var xmlRows = document.Descendants()
                .Where(element => element.Name.LocalName.Equals("DettaglioLinee", StringComparison.Ordinal))
                .Take(50)
                .ToArray();
            var rows = new List<object>();
            var missingArticles = 0;

            for (var index = 0; index < xmlRows.Length; index++)
            {
                var element = xmlRows[index];
                var quantity = ParseXmlDecimal(ChildValue(element, "Quantita"));
                var price = ParseXmlDecimal(ChildValue(element, "PrezzoUnitario"));
                var amount = ParseXmlDecimal(ChildValue(element, "PrezzoTotale"));
                var discountElements = element.Elements()
                    .Where(child => child.Name.LocalName.Equals("ScontoMaggiorazione", StringComparison.Ordinal))
                    .Where(child => ChildValue(child, "Tipo").Equals("SC", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var discount = discountElements
                    .Select(child => ParseXmlDecimal(ChildValue(child, "Percentuale")))
                    .FirstOrDefault(value => value is not null);

                if (discountElements.Length > 0 && quantity is not null && price is not null && amount is not null)
                {
                    var gross = quantity.Value * price.Value;
                    if (gross != 0)
                    {
                        discount = (gross - amount.Value) * 100 / gross;
                    }
                }

                var electronicArticleCode = FirstArticleCode(element);
                var articleMatch = await ResolveArticleCodeAsync(
                    electronicArticleCode,
                    matchedSupplier.Code,
                    cancellationToken);

                if (!articleMatch.Found)
                {
                    missingArticles++;
                }

                rows.Add(new
                {
                    rowNumber = index + 1,
                    articleCode = articleMatch.ArticleCode,
                    electronicArticleCode,
                    articleFound = articleMatch.Found,
                    description = ChildValue(element, "Descrizione"),
                    unitMeasure = ChildValue(element, "UnitaMisura"),
                    quantity = FormatItalianQuantity(quantity),
                    price = FormatItalianAmount(price),
                    discount = FormatItalianPercent(discount),
                    amount = FormatItalianAmount(amount),
                    vatRate = FormatItalianPercent(ParseXmlDecimal(ChildValue(element, "AliquotaIVA")))
                });
            }

            return new JsonResult(new
            {
                success = true,
                fileName = Path.GetFileName(path),
                fullPath = path,
                documentNumber,
                documentDate,
                supplier = new
                {
                    code = matchedSupplier.Code,
                    codeDisplay = matchedSupplier.Code.ToString("00000", CultureInfo.InvariantCulture),
                    name = matchedSupplier.Name,
                    storeCode = matchedSupplier.StoreCode
                },
                missingArticles,
                rows
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException or InvalidDataException)
        {
            return new JsonResult(new { success = false, message = "File XML/P7M non leggibile." });
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
                ? await ElectronicInvoiceArchiveFolderAsync(cancellationToken)
                : await ElectronicInvoiceFolderDefaultAsync(cancellationToken);
            path = Path.Combine(electronicInvoiceFolder, safeFileName);
        }


        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
        {
            var missingFileName = WebUtility.HtmlEncode(Path.GetFileName(fileName ?? path ?? ""));
            var html = """
                <!doctype html>
                <html lang="it">
                <head>
                    <meta charset="utf-8">
                    <style>
                        body {
                            margin: 0;
                            font-family: Arial, sans-serif;
                            background: #f6f8fb;
                            color: #102033;
                        }

                        .message {
                            margin: 42px auto;
                            max-width: 620px;
                            border: 1px solid #9db2cc;
                            background: #ffffff;
                            padding: 22px 26px;
                            box-shadow: 0 6px 18px rgba(16, 32, 51, 0.12);
                        }

                        h1 {
                            margin: 0 0 12px;
                            font-size: 18px;
                            color: #0b3d7a;
                        }

                        p {
                            margin: 0;
                            font-size: 14px;
                            line-height: 1.45;
                        }
                    </style>
                </head>
                <body>
                    <div class="message">
                        <h1>Fattura elettronica non trovata</h1>
                        <p>Il file __FILE_NAME__ non e' presente nella cartella FE acquisti configurata.</p>
                    </div>
                </body>
                </html>
                """.Replace("__FILE_NAME__", missingFileName, StringComparison.Ordinal);
            return Content(html, "text/html; charset=utf-8", Encoding.UTF8);
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
    }

    private static string? NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        return returnUrl.StartsWith('/')
            && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
            ? returnUrl
            : null;
    }

    private static string ValidateSaveCommand(StockLoadSaveCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.DocumentNumber))
        {
            return "Numero documento obbligatorio.";
        }

        if (command.DocumentDate is null)
        {
            return "Data documento obbligatoria.";
        }

        if (command.DocumentDate.Value.Year != command.Year)
        {
            return $"La data documento non appartiene all'esercizio {command.Year}.";
        }

        if (command.CauseCode is not (10 or 12))
        {
            return "Tipo carico non valido.";
        }

        if (command.SupplierCode <= 0)
        {
            return "Fornitore obbligatorio.";
        }

        var rows = command.Rows
            .Where(row => !string.IsNullOrWhiteSpace(row.ArticleCode))
            .ToArray();
        if (rows.Length == 0)
        {
            return "Inserire almeno una riga articolo.";
        }

        var invalidQuantityRow = rows.FirstOrDefault(row => row.Quantity == 0);
        if (invalidQuantityRow is not null)
        {
            return $"Quantita' obbligatoria alla riga {invalidQuantityRow.RowNumber}.";
        }

        return "";
    }

    private static string ValidateOverwriteCheckCommand(StockLoadSaveCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.DocumentNumber))
        {
            return "Numero documento obbligatorio.";
        }

        if (command.DocumentDate is null)
        {
            return "Data documento obbligatoria.";
        }

        if (command.DocumentDate.Value.Year != command.Year)
        {
            return $"La data documento non appartiene all'esercizio {command.Year}.";
        }

        if (command.SupplierCode <= 0)
        {
            return "Fornitore obbligatorio.";
        }

        return "";
    }

    private async Task<string> FirstMissingArticleAsync(
        StockLoadSaveCommand command,
        CancellationToken cancellationToken)
    {
        var codes = command.Rows
            .Select(row => row.ArticleCode.Trim())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (codes.Length == 0)
        {
            return "";
        }

        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        foreach (var code in codes)
        {
            await using var sql = new MySqlCommand(
                "SELECT COUNT(*) FROM articoli WHERE Codice = @code;",
                connection);
            sql.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt32(await sql.ExecuteScalarAsync(cancellationToken)) == 0)
            {
                return code;
            }
        }

        return "";
    }

    private Task<string> ElectronicInvoiceFolderDefaultAsync(CancellationToken cancellationToken)
    {
        servicePaths.EnsureCreated();
        return Task.FromResult(servicePaths.FEAcquistiTransito);
    }

    private Task<string> ElectronicInvoiceArchiveFolderAsync(CancellationToken cancellationToken)
    {
        servicePaths.EnsureCreated();
        return Task.FromResult(servicePaths.FEAcquistiArchivio);
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
            reader["PuntoV"] == DBNull.Value ? null : Convert.ToInt32(reader["PuntoV"]));
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

    private async Task<ArticleMatch> ResolveArticleCodeAsync(
        string electronicArticleCode,
        int supplierCode,
        CancellationToken cancellationToken)
    {
        var code = (electronicArticleCode ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return new ArticleMatch("", false);
        }

        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            """
            SELECT Codice
            FROM articoli
            WHERE Codice = @code
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("@code", code);

        var directCode = await command.ExecuteScalarAsync(cancellationToken);
        if (directCode is not null && directCode != DBNull.Value)
        {
            return new ArticleMatch(Convert.ToString(directCode) ?? code, true);
        }

        command.CommandText = """
            SELECT Codice
            FROM articoli
            WHERE Fornitore = @supplierCode
              AND CodiceFn = @code
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@supplierCode", supplierCode);

        var supplierArticleCode = await command.ExecuteScalarAsync(cancellationToken);
        if (supplierArticleCode is not null && supplierArticleCode != DBNull.Value)
        {
            return new ArticleMatch(Convert.ToString(supplierArticleCode) ?? code, true);
        }

        return new ArticleMatch(code, false);
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

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 KB";
        }

        var kilobytes = Math.Max(1, (int)Math.Round(bytes / 1024m, MidpointRounding.AwayFromZero));
        return $"{kilobytes:N0} KB";
    }

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

    private static XElement? FirstDescendant(XDocument document, string localName) =>
        document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName.Equals(localName, StringComparison.Ordinal));

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

    private static string FirstArticleCode(XElement line)
    {
        return line.Elements()
            .Where(child => child.Name.LocalName.Equals("CodiceArticolo", StringComparison.Ordinal))
            .Select(child => ChildValue(child, "CodiceValore"))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? "";
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

    private static string FormatItalianAmount(decimal? value) =>
        value is null || value == 0
            ? ""
            : value.Value.ToString("#,##0.00", CultureInfo.GetCultureInfo("it-IT"));

    private static string FormatItalianQuantity(decimal? value) =>
        value is null || value == 0
            ? ""
            : value.Value.ToString("#,##0.000", CultureInfo.GetCultureInfo("it-IT"));

    private static string FormatItalianPercent(decimal? value) =>
        value is null || value == 0
            ? ""
            : value.Value.ToString("#,##0.00", CultureInfo.GetCultureInfo("it-IT")) + " %";

    private static string NormalizeFiscalCode(string value) =>
        Regex.Replace(value ?? "", @"[\s\-.]", "").ToUpperInvariant();

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

    private sealed record ElectronicInvoiceArchiveCopyResult(bool Copied, string Warning)
    {
        public static ElectronicInvoiceArchiveCopyResult Empty { get; } = new(false, "");
    }

    private sealed record SupplierMatch(int Code, string Name, int? StoreCode);

    private sealed record CompanyFiscalData(string FiscalCode, string VatNumber);

    private sealed record ArticleMatch(string ArticleCode, bool Found);
}







