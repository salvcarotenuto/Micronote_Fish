using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.Nazioni;

public class IndexModel(NationRepository repository) : PageModel
{
    public IReadOnlyList<NationListItem> Nations { get; private set; } = [];

    [BindProperty]
    public NationEditModel Nation { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? Selected { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Nations = await repository.ListAsync(cancellationToken);
        Nation = Selected is not null
            ? await repository.GetAsync(Selected.Value, cancellationToken) ?? new()
            : FirstNation();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Normalize();

        if (!ModelState.IsValid)
        {
            Nations = await repository.ListAsync(cancellationToken);
            return Page();
        }

        if (Nation.IsNew)
        {
            await repository.InsertAsync(Nation, cancellationToken);
        }
        else
        {
            var updated = await repository.UpdateAsync(Nation, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }
        }

        return RedirectToPage(new { selected = Nation.Code });
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        int code,
        CancellationToken cancellationToken)
    {
        var result = await repository.DeleteAsync(code, cancellationToken);
        if (!result.Deleted)
        {
            ErrorMessage = result.Message;
        }

        return RedirectToPage();
    }

    private NationEditModel FirstNation()
    {
        var first = Nations.FirstOrDefault();
        return first is null
            ? new NationEditModel()
            : new NationEditModel
            {
                IsNew = false,
                Code = first.Code,
                Name = first.Name,
                Abbreviation1 = first.Abbreviation1,
                Abbreviation2 = first.Abbreviation2,
                Iso = first.Iso,
                TaxCode = first.TaxCode,
                Zone = first.Zone,
                Regime = first.Regime
            };
    }

    private void Normalize()
    {
        Nation.Name = ToItalianTitleCase(Nation.Name);
        Nation.Abbreviation1 = Nation.Abbreviation1?.Trim().ToUpperInvariant();
        Nation.Abbreviation2 = Nation.Abbreviation2?.Trim().ToUpperInvariant();
        Nation.Iso = Nation.Iso?.Trim().ToUpperInvariant();
        Nation.TaxCode = Nation.TaxCode?.Trim().ToUpperInvariant();
        Nation.Zone = Nation.Zone?.Trim().ToUpperInvariant();
        Nation.Regime = Nation.Regime?.Trim().ToUpperInvariant();
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
