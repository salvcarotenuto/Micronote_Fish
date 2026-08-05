using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.CausaliCassa;

public sealed class EditModel(CashCauseRepository repository) : PageModel
{
    [BindProperty]
    public CashCauseEditModel Cause { get; set; } = new() { IsNew = true };

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int? code, CancellationToken cancellationToken)
    {
        if (!code.HasValue)
        {
            Cause.Code = await repository.NextCodeAsync(cancellationToken);
            if (Cause.Code == 0)
            {
                ErrorMessage = "Non sono disponibili codici causale liberi.";
                return RedirectToPage("./Index");
            }
            return Page();
        }
        var cause = await repository.GetAsync(code.Value, cancellationToken);
        if (cause is null) return NotFound();
        if (cause.Locked)
        {
            ErrorMessage = "La causale è bloccata e non può essere modificata.";
            return RedirectToPage("./Index");
        }
        Cause = cause;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Normalize();
        if (Cause.IsNew)
        {
            Cause.Code = await repository.NextCodeAsync(cancellationToken);
            ModelState.Remove("Cause.Code");
            if (Cause.Code == 0)
            {
                ModelState.AddModelError("Cause.Code", "Non sono disponibili codici causale liberi.");
            }
        }
        if (!ModelState.IsValid) return Page();
        if (Cause.IsNew)
        {
            await repository.InsertAsync(Cause, cancellationToken);
        }
        else
        {
            var result = await repository.UpdateAsync(Cause, cancellationToken);
            if (!result.Updated)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return Page();
            }
        }
        return RedirectToPage("./Index");
    }

    private void Normalize()
    {
        var culture = CultureInfo.GetCultureInfo("it-IT");
        var description = (Cause.Description?.Trim() ?? "").ToLower(culture);
        Cause.Description = description.Length == 0
            ? ""
            : culture.TextInfo.ToUpper(description[..1]) + description[1..];
        Cause.MovementType = Cause.MovementType?.Trim().ToUpperInvariant() ?? "";
        Cause.SubjectType = Cause.SubjectType?.Trim().ToUpperInvariant() ?? "";
    }
}
