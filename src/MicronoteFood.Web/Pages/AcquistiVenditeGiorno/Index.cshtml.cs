using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.AcquistiVenditeGiorno;

public sealed class IndexModel(DailyTradingRepository repository) : PageModel
{
    public DailyTradingPageModel Report { get; private set; } = new();

    public async Task OnGetAsync(
        DateOnly? movementDate,
        string? grouping,
        CancellationToken cancellationToken = default)
    {
        var date = movementDate
            ?? await repository.LastDateAsync(cancellationToken)
            ?? DateOnly.FromDateTime(DateTime.Today);
        Report = await repository.GetAsync(date, grouping, cancellationToken);
    }
}
