using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.ArticoloPrimaNota;

public sealed class IndexModel(AccountingMovementRepository repository) : PageModel
{
    public AccountingArticleDetailModel? Detail { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        Detail = await repository.GetArticleDetailAsync(id, cancellationToken);
        return Detail is null ? NotFound() : Page();
    }
}
