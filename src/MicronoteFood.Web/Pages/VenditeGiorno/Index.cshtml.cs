using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.VenditeGiorno;

public sealed class IndexModel(DailySalesRepository repository) : PageModel
{
    public DailySalesPageModel Report { get; private set; } = new();

    public async Task OnGetAsync(DateOnly? movementDate, CancellationToken cancellationToken = default)
    {
        var date = movementDate ?? await repository.LastDateAsync(cancellationToken) ?? DateOnly.FromDateTime(DateTime.Today);
        Report = await repository.GetAsync(date, cancellationToken);
    }
}
