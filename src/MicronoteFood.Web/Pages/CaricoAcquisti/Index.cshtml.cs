using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.CaricoAcquisti;

public sealed class IndexModel(
    StockLoadRepository repository,
    ApplicationState applicationState) : PageModel
{
    public StockLoadListPageModel List { get; private set; } = new();

    public async Task OnGetAsync(
        int? year,
        int? month,
        int? supplierCode,
        int? storeCode,
        string? articleCode,
        string? selectedKey,
        CancellationToken cancellationToken)
    {
        List = await repository.GetListAsync(
            year.GetValueOrDefault(applicationState.Esercizio),
            month,
            supplierCode,
            storeCode,
            articleCode,
            selectedKey,
            cancellationToken);
    }

    public async Task<JsonResult> OnGetDetailsAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var details = await repository.GetDetailsAsync(id, cancellationToken);
        return new JsonResult(details.Select(detail => new
        {
            articleCode = detail.ArticleCode,
            description = detail.Description,
            unitMeasure = detail.UnitMeasure,
            quantity = detail.Quantity,
            price = detail.Price,
            discount = detail.Discount,
            amount = detail.Amount,
            vatRate = detail.VatRate,
            tare = detail.Tare,
            netPrice = detail.NetPrice,
            vatIncludedPrice = detail.VatIncludedPrice
        }));
    }
}
