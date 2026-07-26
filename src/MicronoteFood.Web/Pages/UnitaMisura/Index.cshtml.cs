using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.UnitaMisura;

public class IndexModel(UnitMeasureRepository repository) : PageModel
{
    public IReadOnlyList<UnitMeasureListItem> Units { get; private set; } = [];

    [BindProperty]
    public UnitMeasureEditModel Unit { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Selected { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Units = await repository.ListAsync(cancellationToken);
        Unit = !string.IsNullOrWhiteSpace(Selected)
            ? await repository.GetAsync(Selected, cancellationToken) ?? new()
            : FirstUnit();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Normalize();

        if (!ModelState.IsValid)
        {
            Units = await repository.ListAsync(cancellationToken);
            return Page();
        }

        if (Unit.IsNew)
        {
            if (await repository.ExistsAsync(Unit.Code, cancellationToken))
            {
                ModelState.AddModelError("Unit.Code", "Codice già esistente.");
                Units = await repository.ListAsync(cancellationToken);
                return Page();
            }

            await repository.InsertAsync(Unit, cancellationToken);
        }
        else
        {
            var updated = await repository.UpdateAsync(Unit, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }
        }

        return RedirectToPage(new { selected = Unit.Code });
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var result = await repository.DeleteAsync(code, cancellationToken);
        if (!result.Deleted)
        {
            ErrorMessage = result.Message;
        }

        return RedirectToPage();
    }

    private UnitMeasureEditModel FirstUnit()
    {
        var first = Units.FirstOrDefault();
        return first is null
            ? new UnitMeasureEditModel()
            : new UnitMeasureEditModel
            {
                IsNew = false,
                Code = first.Code,
                Description = first.Description
            };
    }

    private void Normalize()
    {
        Unit.Code = Unit.Code?.Trim() ?? "";
        Unit.Description = ToItalianTitleCase(Unit.Description);
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
