using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.RendicontoCassa;

public sealed class IndexModel(CashStatementRepository repository, ApplicationState applicationState) : PageModel
{
    public CashStatementPageModel Report { get; private set; } = new();

    public async Task OnGetAsync(DateOnly? dateFrom, DateOnly? dateTo, int cashType = 0, int storeCode = 0,
        CancellationToken cancellationToken = default)
    {
        var year = applicationState.Esercizio;
        var from = dateFrom ?? new DateOnly(year, DateTime.Today.Year == year ? DateTime.Today.Month : 1, 1);
        var defaultTo = DateTime.Today.Year == year
            ? DateOnly.FromDateTime(DateTime.Today)
            : new DateOnly(year, 12, 31);
        var to = dateTo ?? defaultTo;
        if (to < from) (from, to) = (to, from);
        Report = await repository.GetAsync(from, to, cashType, storeCode, cancellationToken);
    }
}
