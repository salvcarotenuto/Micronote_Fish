using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.ScaricoPerditeResi;

public sealed class EditModel(StockUnloadRepository repository, ApplicationState state) : PageModel
{
    [BindProperty] public StockUnloadEditModel Document { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string ReturnTo { get; set; } = "menu";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public StockUnloadMaskModel Mask { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int? year, int? id, CancellationToken ct)
    {
        if (id > 0)
        {
            var result = await repository.GetEditAsync(id.Value, ct);
            if (result is null) return RedirectToPage("./Index");
            Document = result.Value.Document; Mask = result.Value.Mask;
        }
        else
        {
            Document.Year = state.Esercizio;
            Document.IsNew = true;
            Document.Code = await repository.NextCodeAsync(Document.Year, ct);
            Document.Date = Document.Year == DateTime.Today.Year
                ? DateOnly.FromDateTime(DateTime.Today) : new DateOnly(Document.Year, 1, 1);
            Mask = await repository.GetMaskAsync(ct);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Document.Date.Year != state.Esercizio)
        {
            ModelState.AddModelError(
                "",
                $"La data movimento deve rientrare nell'esercizio contabile in linea ({state.Esercizio}).");
            Mask = await repository.GetMaskAsync(ct);
            return Page();
        }

        var result = await repository.SaveAsync(Document, ct);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error);
            Mask = await repository.GetMaskAsync(ct);
            return Page();
        }
        if (ReturnTo.Equals("list", StringComparison.OrdinalIgnoreCase))
            return LocalRedirect(SafeReturnUrl() ?? Url.Page("./Index", new { year = Document.Year, selectedKey = result.Id.ToString() })!);
        return RedirectToPage("./Edit", new { year = Document.Year, returnTo = "menu" });
    }

    public async Task<JsonResult> OnGetStockAsync(string articleCode, int id, CancellationToken ct) =>
        new(new { stock = await repository.GetStockAsync(articleCode ?? "", id, ct) });

    public IActionResult OnPostCancel() =>
        ReturnTo.Equals("list", StringComparison.OrdinalIgnoreCase)
            ? LocalRedirect(SafeReturnUrl() ?? "/ScaricoPerditeResi")
            : RedirectToPage("/Index");

    private string? SafeReturnUrl() => !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : null;
}
