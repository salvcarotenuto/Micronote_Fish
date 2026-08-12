using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.MovimentoContabileCliente;

public sealed class EditModel(
    CustomerCashMovementRepository repository,
    LookupRepository lookupRepository,
    ApplicationState applicationState) : PageModel
{
    [BindProperty] public CustomerCashMovementEditModel Movement { get; set; } = new();
    public CustomerCashMovementMaskModel Mask { get; private set; } = new();
    public string ReturnTo { get; private set; } = "";
    public string ReturnUrl { get; private set; } = "/";
    public bool CustomerReadOnly { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        int? id,
        int? customerCode,
        bool customerReadOnly,
        string? returnTo,
        CancellationToken cancellationToken)
    {
        SetReturnTarget(returnTo);
        CustomerReadOnly = customerReadOnly;
        Mask = await repository.GetMaskAsync(cancellationToken);
        if (id.GetValueOrDefault() > 0)
        {
            var existing = await repository.GetAsync(id!.Value, cancellationToken);
            if (existing is null) return NotFound();
            Movement = existing;
            return Page();
        }

        Movement = new CustomerCashMovementEditModel
        {
            IsNew = true,
            Year = applicationState.Esercizio,
            Sector = 40,
            MovementDate = applicationState.Esercizio == DateTime.Today.Year
                ? DateOnly.FromDateTime(DateTime.Today)
                : new DateOnly(applicationState.Esercizio, 1, 1),
            CauseCode = 20
        };
        if (customerCode.GetValueOrDefault() > 0)
        {
            Movement.CustomerCode = customerCode!.Value;
            var customer = await lookupRepository.FindAnagraficaAsync(
                "clienti", customerCode.Value, cancellationToken);
            if (customer is not null)
            {
                Movement.CustomerName = customer.Label;
                Movement.CustomerStoreCode = customer.StoreCode ?? 0;
            }
        }
        return Page();
    }

    public async Task<IActionResult> OnGetDocumentsAsync(
        string type,
        int customerCode,
        int year,
        CancellationToken cancellationToken)
    {
        var rows = await repository.ListDocumentsAsync(
            type?.Trim().ToUpperInvariant() ?? "",
            customerCode,
            year <= 0 ? applicationState.Esercizio : year,
            cancellationToken);
        return new JsonResult(rows.Select(row => new
        {
            id = row.Id,
            year = row.Year,
            code = row.Code,
            number = row.Number,
            date = row.Date.ToString("dd/MM/yyyy"),
            total = row.Total
        }));
    }
    public async Task<IActionResult> OnPostAsync(
        string? returnTo,
        bool customerReadOnly,
        CancellationToken cancellationToken)
    {
        SetReturnTarget(returnTo);
        CustomerReadOnly = customerReadOnly;
        Mask = await repository.GetMaskAsync(cancellationToken);
        if (!Movement.IsNew && Movement.Id > 0)
        {
            var existing = await repository.GetAsync(Movement.Id, cancellationToken);
            if (existing is null) return NotFound();
            Movement.Year = existing.Year;
        }
        Normalize();
        ValidateMovement();
        if (!ModelState.IsValid) return Page();

        var result = await repository.SaveAsync(Movement, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error);
            return Page();
        }

        if (string.Equals(ReturnTo, "menu", StringComparison.OrdinalIgnoreCase))
            return RedirectToPage("./Edit", new { returnTo = "menu" });
        if (ReturnTo.StartsWith('/') && !ReturnTo.StartsWith("//", StringComparison.Ordinal))
        {
            var separator = ReturnTo.Contains('?') ? '&' : '?';
            return LocalRedirect($"{ReturnTo}{separator}refresh={DateTime.UtcNow.Ticks}");
        }

        return RedirectToPage("./Edit", new { id = result.Id });
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
        if (target.StartsWith('/')
            && !target.StartsWith("//", StringComparison.Ordinal)
            && !target.Contains("://", StringComparison.Ordinal))
        {
            ReturnTo = target;
            ReturnUrl = target;
            return;
        }

        ReturnTo = "";
        ReturnUrl = "/";
    }

    private void Normalize()
    {
        Movement.Year = Movement.Year <= 0 ? applicationState.Esercizio : Movement.Year;
        Movement.Sector = 40;
        Movement.CustomerName = Movement.CustomerName?.Trim() ?? "";
        Movement.Description = Movement.Description?.Trim() ?? "";
        Movement.DocumentType = Movement.DocumentType?.Trim().ToUpperInvariant() ?? "";
        if (Movement.DocumentId.GetValueOrDefault() <= 0)
        {
            Movement.DocumentId = null;
            Movement.DocumentType = "";
        }
        else if (Movement.DocumentType?.Length == 0)
        {
            Movement.DocumentType = "B";
        }
    }

    private void ValidateMovement()
    {
        var requiredYear = Movement.IsNew
            ? applicationState.Esercizio
            : Movement.Year;
        if (Movement.MovementDate == default)
            ModelState.AddModelError("", "Campo Data movimento obbligatorio.");
        else if (Movement.MovementDate.Year != requiredYear)
            ModelState.AddModelError("", Movement.IsNew
                ? $"La data movimento deve rientrare nell'esercizio contabile in linea ({requiredYear})."
                : $"La data movimento deve rientrare nell'anno registrato nel movimento ({requiredYear}).");
        if (Movement.CustomerCode <= 0)
            ModelState.AddModelError("", "Campo Cliente obbligatorio.");
        if (Movement.CauseCode is not (5 or 20))
            ModelState.AddModelError("", "Causale movimento non valida.");
        if (Movement.Amount <= 0)
            ModelState.AddModelError("", "Campo Importo obbligatorio.");
        if (Movement.DocumentType is not ("" or "B"))
            ModelState.AddModelError("", "Tipo documento collegato non valido.");
        if (Movement.DocumentId.HasValue && Movement.DocumentType?.Length == 0)
            ModelState.AddModelError("", "Indicare il tipo del documento collegato.");
        if (Movement.PaymentMethod is < 0 or > 2)
            ModelState.AddModelError("", "Modo di pagamento non valido.");
        if (Movement.Description?.Length > 100)
            ModelState.AddModelError("", "Le annotazioni non possono superare 100 caratteri.");
    }
}
