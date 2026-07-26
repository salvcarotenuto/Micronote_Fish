using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.Vendite;

public class StoricoModel(
    SalesHistoryRepository repository,
    SalesEntryRepository salesEntryRepository,
    ApplicationState applicationState) : PageModel
{
    public SalesHistoryPageModel History { get; private set; } = new();

    public async Task OnGetAsync(
        int? year,
        int? month,
        CancellationToken cancellationToken)
    {
        var filterYear = year ?? applicationState.Esercizio;
        var filterMonth = Math.Clamp(month ?? 0, 0, 12);
        var (start, end) = PeriodFrom(filterYear, filterMonth);

        History = await repository.GetAsync(
            filterYear,
            filterMonth,
            start,
            end,
            cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        int saleId,
        int? year,
        int? month,
        CancellationToken cancellationToken)
    {
        await salesEntryRepository.DeleteByIdAsync(saleId, cancellationToken);
        return RedirectToPage(
            "./Storico",
            new
            {
                year,
                month
            });
    }

    public async Task<JsonResult> OnGetDetailsAsync(
        int year,
        int code,
        CancellationToken cancellationToken)
    {
        var details = await repository.ListDetailsAsync(year, code, cancellationToken);
        return new JsonResult(new
        {
            rows = details.Select(row => new
            {
                storeCode = row.StoreCode,
                storeName = row.StoreName,
                net = row.Net,
                nonTaxable = row.NonTaxable,
                vat = row.Vat,
                total = row.Total,
                cash = row.Cash,
                card = row.Card,
                tickets = row.Tickets,
                checks = row.Checks,
                other = row.Other,
                suspended = row.Suspended,
                losses = row.Losses
            })
        });
    }

    private static (DateOnly Start, DateOnly End) PeriodFrom(int year, int month)
    {
        if (month is >= 1 and <= 12)
        {
            return (
                new DateOnly(year, month, 1),
                new DateOnly(year, month, DateTime.DaysInMonth(year, month)));
        }

        return (new DateOnly(year, 1, 1), DefaultEndDate(year));
    }

    private static DateOnly DefaultEndDate(int year)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return today.Year == year ? today : new DateOnly(year, 12, 31);
    }
}
