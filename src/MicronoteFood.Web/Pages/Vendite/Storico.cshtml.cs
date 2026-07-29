using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.Vendite;

public class StoricoModel(
    SalesHistoryRepository repository,
    ApplicationState applicationState) : PageModel
{
    public SalesHistoryPageModel History { get; private set; } = new();

    public async Task OnGetAsync(
        int? year,
        int? month,
        int? customer,
        int? store,
        CancellationToken cancellationToken)
    {
        var filterYear = year ?? applicationState.Esercizio;
        var filterMonth = Math.Clamp(month ?? 0, 0, 12);
        History = await repository.GetAsync(
            filterYear,
            filterMonth,
            customer ?? 0,
            store ?? 0,
            cancellationToken);
    }

    public async Task<JsonResult> OnGetDetailsAsync(
        int saleId,
        CancellationToken cancellationToken)
    {
        var details = await repository.ListDetailsAsync(saleId, cancellationToken);
        return new JsonResult(new
        {
            rows = details.Select(row => new
            {
                rowNumber = row.RowNumber,
                articleCode = row.ArticleCode,
                description = row.Description,
                unit = row.Unit,
                packages = row.Packages,
                tare = row.Tare,
                quantity = row.Quantity,
                price = row.Price,
                vatRate = row.VatRate,
                vatPrice = row.VatPrice,
                amount = row.Amount
            })
        });
    }
}
