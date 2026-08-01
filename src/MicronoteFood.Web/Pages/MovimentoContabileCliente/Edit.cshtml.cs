using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.MovimentoContabileCliente;

public sealed class EditModel(
    CustomerCashMovementRepository repository,
    ApplicationState applicationState) : PageModel
{
    [BindProperty] public CustomerCashMovementEditModel Movement { get; set; } = new();
    public CustomerCashMovementMaskModel Mask { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken cancellationToken)
    {
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
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Mask = await repository.GetMaskAsync(cancellationToken);
        Normalize();
        ValidateMovement();
        if (!ModelState.IsValid) return Page();

        var result = await repository.SaveAsync(Movement, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error);
            return Page();
        }

        TempData["SuccessMessage"] = Movement.IsNew
            ? "Movimento contabile cliente registrato."
            : "Movimento contabile cliente aggiornato.";
        return RedirectToPage("./Edit", new { id = result.Id });
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
        else if (Movement.DocumentType.Length == 0)
        {
            Movement.DocumentType = "B";
        }
    }

    private void ValidateMovement()
    {
        if (Movement.MovementDate == default)
            ModelState.AddModelError("", "Campo Data movimento obbligatorio.");
        else if (Movement.MovementDate.Year != applicationState.Esercizio)
            ModelState.AddModelError("", $"La data movimento deve rientrare nell'esercizio contabile in linea ({applicationState.Esercizio}).");
        if (Movement.CustomerCode <= 0)
            ModelState.AddModelError("", "Campo Cliente obbligatorio.");
        if (Movement.CauseCode is not (5 or 20))
            ModelState.AddModelError("", "Causale movimento non valida.");
        if (Movement.Amount <= 0)
            ModelState.AddModelError("", "Campo Importo obbligatorio.");
        if (Movement.DocumentType is not ("" or "B"))
            ModelState.AddModelError("", "Tipo documento collegato non valido.");
        if (Movement.DocumentId.HasValue && Movement.DocumentType.Length == 0)
            ModelState.AddModelError("", "Indicare il tipo del documento collegato.");
        if (Movement.PaymentMethod is < 0 or > 2)
            ModelState.AddModelError("", "Modo di pagamento non valido.");
        if (Movement.Description.Length > 100)
            ModelState.AddModelError("", "Le annotazioni non possono superare 100 caratteri.");
    }
}
