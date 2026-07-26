using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.PrimaNota;

public sealed class EditModel(
    AccountingMovementRepository movementRepository,
    AccountingCauseRepository causeRepository,
    ApplicationState applicationState) : PageModel
{
    [BindProperty]
    public AccountingMovementEditModel Movement { get; set; } = new();

    [BindProperty]
    public int Azione { get; set; } = FormAzione.Inserimento;

    public bool IsNew => FormAzione.IsInserimento(Azione);

    public IReadOnlyList<AccountingCauseAccountOption> AccountOptions { get; private set; } = [];

    public IReadOnlyList<AccountingMovementCauseTemplate> CauseTemplates { get; private set; } = [];

    public IReadOnlyList<AccountingMovementStoreOption> StoreOptions { get; private set; } = [];

    public string ReturnTo { get; private set; } = "";

    public string ReturnUrl { get; private set; } = "/PrimaNota";

    public async Task<IActionResult> OnGetAsync(
        int? id,
        int? azione,
        string? returnTo,
        CancellationToken cancellationToken)
    {
        SetReturnTarget(returnTo);
        Azione = ResolveAzione(azione, id.HasValue);

        if (id.HasValue)
        {
            var movement = await movementRepository.GetEditModelAsync(
                id.Value,
                applicationState.Esercizio,
                cancellationToken);
            if (movement is null)
            {
                return NotFound();
            }

            Movement = movement;
        }
        else
        {
            Movement = await movementRepository.NewEditModelAsync(
                applicationState.Esercizio,
                cancellationToken);
        }

        Movement.IsNew = IsNew;

        await LoadLookupsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        string? returnTo,
        CancellationToken cancellationToken)
    {
        SetReturnTarget(returnTo);
        Movement.IsNew = IsNew;
        await LoadLookupsAsync(cancellationToken);
        NormalizePostedMovement();
        ClearOptionalModelStateErrors();
        ValidateMovement();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var id = await movementRepository.SaveAsync(
            Movement,
            applicationState.Esercizio,
            cancellationToken);
        if (string.Equals(ReturnTo, "menu", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage("./Edit", new { returnTo = "menu" });
        }

        if (!string.IsNullOrWhiteSpace(ReturnTo))
        {
            return Redirect(ReturnUrl);
        }

        return RedirectToPage(
            "./Index",
            new
            {
                selectedId = id,
                dateFrom = new DateOnly(Movement.Year, 1, 1).ToString("yyyy-MM-dd"),
                dateTo = new DateOnly(Movement.Year, 12, 31).ToString("yyyy-MM-dd")
            });
    }

    private void SetReturnTarget(string? returnTo)
    {
        if (string.Equals(returnTo, "menu", StringComparison.OrdinalIgnoreCase))
        {
            ReturnTo = "menu";
            ReturnUrl = "/";
            return;
        }

        var target = returnTo?.Trim() ?? "";
        if (IsLocalReturnUrl(target))
        {
            ReturnTo = target;
            ReturnUrl = target;
            return;
        }

        ReturnTo = "";
        ReturnUrl = "/PrimaNota";
    }

    private static bool IsLocalReturnUrl(string value) =>
        value.StartsWith('/') &&
        !value.StartsWith("//", StringComparison.Ordinal) &&
        !value.Contains("://", StringComparison.Ordinal);

    private static int ResolveAzione(int? azione, bool hasRecord)
    {
        if (azione is > 0)
        {
            return FormAzione.Normalize(azione.Value, FormAzione.ForRecord(hasRecord));
        }

        return FormAzione.ForRecord(hasRecord);
    }

    public async Task<IActionResult> OnGetLinkedDocumentsAsync(
        string kind,
        int year,
        string subjectType,
        int subjectCode,
        CancellationToken cancellationToken)
    {
        var documents = await movementRepository.ListLinkedDocumentsAsync(
            kind,
            year <= 0 ? applicationState.Esercizio : year,
            subjectType,
            subjectCode,
            cancellationToken);

        return new JsonResult(documents.Select(document => new
        {
            id = document.Id,
            year = document.Year,
            sector = document.Sector,
            code = document.Code,
            protocol = $"{document.Code:000000} / {document.Year}",
            number = document.Number,
            date = document.Date?.ToString("dd-MM-yyyy") ?? "",
            amount = document.Amount
        }));
    }

    public AccountingCauseAccountOption? GetAccount(int? code) =>
        AccountOptions.FirstOrDefault(option => option.AccountCode == code.GetValueOrDefault());

    private async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        AccountOptions = await causeRepository.GetAccountOptionsAsync(cancellationToken);
        CauseTemplates = await movementRepository.ListCauseTemplatesAsync(cancellationToken);
        StoreOptions = await movementRepository.ListStoresAsync(cancellationToken);
    }

    private void ClearOptionalModelStateErrors()
    {
        foreach (var key in new[]
        {
            "Movement.DocumentNumber",
            "Movement.SubjectType",
            "Movement.SubjectName",
            "Movement.Notes",
            "Movement.CauseDescription"
        })
        {
            ModelState.Remove(key);
        }

        for (var index = 0; index < 6; index++)
        {
            ModelState.Remove($"Movement.DebitLines[{index}].Amount");
            ModelState.Remove($"Movement.CreditLines[{index}].Amount");
        }
    }

    private void NormalizePostedMovement()
    {
        Movement.Year = Movement.Year <= 0 ? applicationState.Esercizio : Movement.Year;
        Movement.Sector = Movement.Sector <= 0 ? 40 : Movement.Sector;
        if (Movement.LinkedInvoiceDocumentId.GetValueOrDefault() <= 0 ||
            Movement.LinkedInvoiceDocumentSector.GetValueOrDefault() <= 0)
        {
            Movement.LinkedInvoiceDocumentId = null;
            Movement.LinkedInvoiceDocumentSector = null;
        }

        if (Movement.LinkedDueDateDocumentId.GetValueOrDefault() <= 0 ||
            Movement.LinkedDueDateDocumentSector.GetValueOrDefault() <= 0)
        {
            Movement.LinkedDueDateDocumentId = null;
            Movement.LinkedDueDateDocumentSector = null;
        }

        Movement.DebitLines = NormalizeLines(Movement.DebitLines, "D");
        Movement.CreditLines = NormalizeLines(Movement.CreditLines, "A");

        var cause = CauseTemplates.FirstOrDefault(row => row.Code == Movement.CauseCode);
        if (cause is not null)
        {
            Movement.CauseDescription = cause.Description;
            Movement.SubjectType = cause.Subject;
        }
    }

    private static List<AccountingMovementLineEditModel> NormalizeLines(
        List<AccountingMovementLineEditModel>? lines,
        string sign)
    {
        var normalized = (lines ?? [])
            .Take(6)
            .Select((line, index) => new AccountingMovementLineEditModel
            {
                RowNumber = index + 1,
                Sign = sign,
                AccountCode = line.AccountCode.GetValueOrDefault() > 0 ? line.AccountCode : null,
                Amount = line.Amount
            })
            .ToList();

        while (normalized.Count < 6)
        {
            normalized.Add(new AccountingMovementLineEditModel
            {
                RowNumber = normalized.Count + 1,
                Sign = sign
            });
        }

        return normalized;
    }

    private void ValidateMovement()
    {
        if (Movement.MovementDate == default)
        {
            ModelState.AddModelError("", "Campo Data documento obbligatorio.");
        }
        else if (Movement.MovementDate.Year != Movement.Year)
        {
            ModelState.AddModelError("", "La data movimento non appartiene all'esercizio contabile in linea.");
        }

        if (Movement.CauseCode is null)
        {
            ModelState.AddModelError("", "Campo Causale obbligatorio.");
        }
        else if (CauseTemplates.All(row => row.Code != Movement.CauseCode.Value))
        {
            ModelState.AddModelError("", "Causale non valida.");
        }

        if (Movement.Amount <= 0)
        {
            ModelState.AddModelError("", "Campo Importo movimento obbligatorio.");
        }

        ValidateCompleteLines(Movement.DebitLines, "Dare");
        ValidateCompleteLines(Movement.CreditLines, "Avere");

        if (Movement.DebitLines.All(line => line.AccountCode.GetValueOrDefault() <= 0))
        {
            ModelState.AddModelError("", "Almeno un conto Dare obbligatorio.");
        }

        if (Movement.DebitLines.All(line => line.Amount == 0))
        {
            ModelState.AddModelError("", "Almeno un importo Dare obbligatorio.");
        }

        if (Movement.CreditLines.All(line => line.AccountCode.GetValueOrDefault() <= 0))
        {
            ModelState.AddModelError("", "Almeno un conto Avere obbligatorio.");
        }

        if (Movement.CreditLines.All(line => line.Amount == 0))
        {
            ModelState.AddModelError("", "Almeno un importo Avere obbligatorio.");
        }

        var debitTotal = Movement.DebitLines.Sum(line => line.Amount);
        var creditTotal = Movement.CreditLines.Sum(line => line.Amount);
        if (Math.Round(debitTotal, 2) != Math.Round(creditTotal, 2))
        {
            ModelState.AddModelError("", "Totale Dare e totale Avere non coincidono.");
        }

        if (Movement.Amount > 0 && Math.Round(Movement.Amount, 2) != Math.Round(debitTotal, 2))
        {
            ModelState.AddModelError("", "Importo movimento diverso dal totale Dare/Avere.");
        }
    }

    private void ValidateCompleteLines(
        IReadOnlyList<AccountingMovementLineEditModel> lines,
        string sideLabel)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var hasAccount = line.AccountCode.GetValueOrDefault() > 0;
            var hasAmount = line.Amount != 0;

            if (hasAccount && !hasAmount)
            {
                ModelState.AddModelError("", $"Riga {sideLabel} {index + 1}: indicare l'importo.");
            }
            else if (!hasAccount && hasAmount)
            {
                ModelState.AddModelError("", $"Riga {sideLabel} {index + 1}: indicare il conto.");
            }
        }
    }
}

