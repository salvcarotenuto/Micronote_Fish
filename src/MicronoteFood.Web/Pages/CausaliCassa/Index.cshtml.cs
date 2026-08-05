using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.CausaliCassa;

public sealed class IndexModel(CashCauseRepository repository) : PageModel
{
    public IReadOnlyList<CashCauseListItem> Causes { get; private set; } = [];

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Causes = await repository.ListAsync(cancellationToken);

    public async Task<IActionResult> OnPostDeleteAsync(int code, CancellationToken cancellationToken)
    {
        var result = await repository.DeleteAsync(code, cancellationToken);
        if (!result.Deleted) ErrorMessage = result.Message;
        return RedirectToPage();
    }
}
