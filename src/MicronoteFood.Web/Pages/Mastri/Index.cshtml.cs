using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.Mastri;

public class IndexModel(LedgerMasterRepository repository) : PageModel
{
    public IReadOnlyList<LedgerMasterListItem> Masters { get; private set; } = [];

    public IReadOnlyList<LedgerMasterTypeOption> TypeOptions { get; } =
        LedgerMasterRepository.TypeOptions;

    public int NextCode { get; private set; }

    [BindProperty]
    public LedgerMasterEditModel Master { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? Selected { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Masters = await repository.ListAsync(cancellationToken);
        NextCode = await repository.NextCodeAsync(cancellationToken);
        Master = Selected.HasValue
            ? await repository.GetAsync(Selected.Value, cancellationToken) ?? new()
            : FirstMaster();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Normalize();
        if (Master.IsNew)
        {
            Master.Code = await repository.NextCodeAsync(cancellationToken);
            ModelState.Remove("Master.Code");
        }

        if (!ModelState.IsValid)
        {
            Masters = await repository.ListAsync(cancellationToken);
            NextCode = await repository.NextCodeAsync(cancellationToken);
            return Page();
        }

        if (Master.IsNew)
        {
            await repository.InsertAsync(Master, cancellationToken);
        }
        else
        {
            var updated = await repository.UpdateAsync(Master, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }
        }

        return RedirectToPage(new { selected = Master.Code });
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

    private LedgerMasterEditModel FirstMaster()
    {
        var first = Masters.FirstOrDefault();
        return first is null
            ? new LedgerMasterEditModel()
            : new LedgerMasterEditModel
            {
                IsNew = false,
                Code = first.Code,
                Description = first.Description,
                Type = first.Type,
                Locked = first.Locked
            };
    }

    private void Normalize()
    {
        Master.Description = ToItalianTitleCase(Master.Description);
        Master.Type = Master.Type?.Trim().ToUpperInvariant() ?? "";
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
