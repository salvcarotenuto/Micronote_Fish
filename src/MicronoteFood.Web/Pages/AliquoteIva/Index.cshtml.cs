using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.AliquoteIva;

public class IndexModel(VatRateRepository repository) : PageModel
{
    public IReadOnlyList<VatRateListItem> Rates { get; private set; } = [];

    public IReadOnlyList<VatNatureOption> NatureOptions { get; private set; } = [];

    [BindProperty]
    public VatRateEditModel Rate { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Selected { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadPageDataAsync(cancellationToken);
        Rate = !string.IsNullOrWhiteSpace(Selected)
            ? await repository.GetAsync(Selected, cancellationToken) ?? new()
            : FirstRate();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Normalize();

        if (!ModelState.IsValid)
        {
            await LoadPageDataAsync(cancellationToken);
            return Page();
        }

        if (Rate.IsNew)
        {
            if (await repository.ExistsAsync(Rate.Code, cancellationToken))
            {
                ModelState.AddModelError("Rate.Code", "Codice già esistente.");
                await LoadPageDataAsync(cancellationToken);
                return Page();
            }

            await repository.InsertAsync(Rate, cancellationToken);
        }
        else
        {
            var updated = await repository.UpdateAsync(Rate, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }
        }

        return RedirectToPage(new { selected = Rate.Code });
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

    public static string PercentText(decimal value) =>
        value.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"));

    private VatRateEditModel FirstRate()
    {
        var first = Rates.FirstOrDefault();
        return first is null
            ? new VatRateEditModel()
            : new VatRateEditModel
            {
                IsNew = false,
                Code = first.Code,
                Description = first.Description,
                Rate = first.Rate,
                Deduction = first.Deduction,
                ElectronicInvoiceNature = first.ElectronicInvoiceNature
            };
    }

    private async Task LoadPageDataAsync(CancellationToken cancellationToken)
    {
        Rates = await repository.ListAsync(cancellationToken);
        NatureOptions = await repository.ListNatureOptionsAsync(cancellationToken);
    }

    private void Normalize()
    {
        Rate.Code = Rate.Code?.Trim() ?? "";
        Rate.Description = ToItalianTitleCase(Rate.Description);
        Rate.ElectronicInvoiceNature = Rate.ElectronicInvoiceNature?.Trim().ToUpperInvariant();
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
