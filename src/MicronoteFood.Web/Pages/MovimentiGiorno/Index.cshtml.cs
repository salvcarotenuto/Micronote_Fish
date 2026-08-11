using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.MovimentiGiorno;

public sealed class IndexModel(DailyMovementRepository repository) : PageModel
{
    public DailyMovementPageModel Report { get; private set; } = new();

    public async Task OnGetAsync(
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? selectionType,
        string? articleCode,
        int? classificationCode,
        CancellationToken cancellationToken = default)
    {
        var lastDate = await repository.LastDateAsync(cancellationToken) ?? DateOnly.FromDateTime(DateTime.Today);
        var from = dateFrom ?? lastDate;
        var to = dateTo ?? from;
        if (from > to) (from, to) = (to, from);
        Report = await repository.GetAsync(from, to, selectionType, articleCode, classificationCode, cancellationToken);
    }
}
