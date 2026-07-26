using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.Comuni;

public class IndexModel(ComuniRepository repository) : PageModel
{
    public IReadOnlyList<ComuneListItem> Comuni { get; private set; } = [];

    [BindProperty]
    public ComuneEditModel Comune { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Selected { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Comuni = await repository.ListAsync(cancellationToken);
        Comune = !string.IsNullOrWhiteSpace(Selected)
            ? await repository.GetAsync(Selected, cancellationToken) ?? new()
            : FirstComune();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Normalize();

        if (!ModelState.IsValid)
        {
            Comuni = await repository.ListAsync(cancellationToken);
            return Page();
        }

        if (Comune.IsNew)
        {
            if (await repository.ExistsAsync(Comune.Name, cancellationToken))
            {
                ModelState.AddModelError("Comune.Name", "Comune già esistente.");
                Comuni = await repository.ListAsync(cancellationToken);
                return Page();
            }

            await repository.InsertAsync(Comune, cancellationToken);
        }
        else
        {
            var updated = await repository.UpdateAsync(Comune, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }
        }

        return RedirectToPage(new { selected = Comune.Name });
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var result = await repository.DeleteAsync(name, cancellationToken);
        if (!result.Deleted)
        {
            ErrorMessage = result.Message;
        }

        return RedirectToPage();
    }

    private ComuneEditModel FirstComune()
    {
        var first = Comuni.FirstOrDefault();
        return first is null
            ? new ComuneEditModel()
            : new ComuneEditModel
            {
                IsNew = false,
                OriginalName = first.Name,
                Name = first.Name,
                Province = first.Province,
                PostalCode = first.PostalCode,
                Code = first.Code
            };
    }

    private void Normalize()
    {
        Comune.OriginalName = Comune.OriginalName?.Trim() ?? "";
        Comune.Name = ToItalianTitleCase(Comune.Name);
        Comune.Province = Comune.Province?.Trim().ToUpperInvariant();
        Comune.PostalCode = Comune.PostalCode?.Trim();
        Comune.Code = Comune.Code?.Trim();
    }

    private static string ToItalianTitleCase(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        var culture = CultureInfo.GetCultureInfo("it-IT");
        return culture.TextInfo.ToTitleCase(trimmed.ToLower(culture));
    }
}
