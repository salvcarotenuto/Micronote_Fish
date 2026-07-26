using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.MovimentiMagazzino;

public sealed class IndexModel(
    StockMovementRepository repository,
    ApplicationState applicationState) : PageModel
{
    public StockMovementListPageModel List { get; private set; } = new();

    public async Task OnGetAsync(
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? articleCode,
        int? categoryCode,
        int? groupCode,
        int? subgroupCode,
        bool? includeLoads,
        bool? includeUnloads,
        int? customerCode,
        int? supplierCode,
        string? selectedKey,
        CancellationToken cancellationToken)
    {
        var year = applicationState.Esercizio;
        var start = dateFrom ?? new DateOnly(year, 1, 1);
        var end = dateTo ?? new DateOnly(year, 1, DateTime.DaysInMonth(year, 1));

        List = await repository.GetListAsync(
            year,
            start,
            end,
            articleCode,
            categoryCode,
            groupCode,
            subgroupCode,
            includeLoads ?? true,
            includeUnloads ?? true,
            customerCode,
            supplierCode,
            selectedKey,
            cancellationToken);
    }
}
