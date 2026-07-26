using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.PianoConti;

public class IndexModel(ChartAccountRepository repository) : PageModel
{
    public IReadOnlyList<ChartAccountListItem> Accounts { get; private set; } = [];

    public SelectList Masters { get; private set; } = EmptySelectList();

    public IReadOnlyList<ChartAccountTypeOption> TypeOptions { get; } =
        ChartAccountRepository.TypeOptions;

    [BindProperty(SupportsGet = true)]
    public int? MasterCode { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Type { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        NormalizeFilters();
        await LoadAsync(cancellationToken);
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

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Accounts = await repository.ListAsync(MasterCode, Type, Search, cancellationToken);
        Masters = Select(await repository.GetMasterOptionsAsync(cancellationToken));
    }

    private void NormalizeFilters()
    {
        Type = Type?.Trim().ToUpperInvariant();
        Search = Search?.Trim();
    }

    private static SelectList Select(IReadOnlyList<LookupOption> items) =>
        new(items, nameof(LookupOption.Code), nameof(LookupOption.Description));

    private static SelectList EmptySelectList() =>
        new(Array.Empty<LookupOption>());
}
