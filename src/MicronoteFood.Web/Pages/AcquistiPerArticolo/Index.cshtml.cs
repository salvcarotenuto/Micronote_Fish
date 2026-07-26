using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.AcquistiPerArticolo;

public sealed class IndexModel(
    StockPurchaseStatsRepository repository,
    ApplicationState applicationState) : PageModel
{
    public StockPurchaseStatsPageModel Report { get; private set; } = new();

    public async Task OnGetAsync(
        bool run,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        int? storeCode,
        string? search,
        CancellationToken cancellationToken)
    {
        var year = applicationState.Esercizio;
        var start = dateFrom ?? new DateOnly(year, 1, 1);
        var end = dateTo ?? DateOnly.FromDateTime(DateTime.Today);
        if (end.Year != year)
        {
            end = new DateOnly(year, 12, 31);
        }

        Report = run
            ? await repository.GetAsync(start, end, storeCode, search, cancellationToken)
            : await repository.GetInitialAsync(start, end, storeCode, search, cancellationToken);
    }

    public async Task<JsonResult> OnGetSuppliersAsync(
        string articleCode,
        DateOnly dateFrom,
        DateOnly dateTo,
        int? storeCode,
        CancellationToken cancellationToken)
    {
        var rows = await repository.ListSuppliersAsync(articleCode, dateFrom, dateTo, storeCode, cancellationToken);
        return new JsonResult(new
        {
            rows = rows.Select(row => new
            {
                supplierCode = row.SupplierCode,
                supplierName = row.SupplierName,
                quantity = row.Quantity,
                amount = row.Amount,
                averageCost = row.AverageCost,
                lastPrice = row.LastPrice,
                lastPurchaseDate = row.LastPurchaseDate?.ToString("yyyy-MM-dd")
            })
        });
    }
}
