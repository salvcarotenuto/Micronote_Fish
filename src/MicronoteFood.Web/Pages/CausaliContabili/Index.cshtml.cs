using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.CausaliContabili;

public class IndexModel(AccountingCauseRepository repository) : PageModel
{
    public IReadOnlyList<AccountingCauseListItem> Causes { get; private set; } = [];

    public IReadOnlyList<AccountingCauseOption> MovementTypeOptions { get; } =
        AccountingCauseRepository.MovementTypeOptions;

    [BindProperty(SupportsGet = true)]
    public string? MovementType { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        NormalizeFilters();
        Causes = await repository.ListAsync(MovementType, Search, cancellationToken);
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

    private void NormalizeFilters()
    {
        MovementType = MovementType?.Trim().ToUpperInvariant();
        Search = Search?.Trim();
    }
}
