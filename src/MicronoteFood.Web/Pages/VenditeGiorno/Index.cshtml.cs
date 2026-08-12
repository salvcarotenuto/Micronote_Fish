using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.VenditeGiorno;

public sealed class IndexModel(DailySalesRepository repository) : PageModel
{
    public DailySalesPageModel Report { get; private set; } = new();

    public async Task OnGetAsync(DateOnly? saleDate, int? storeCode, string? selectedKey, CancellationToken cancellationToken = default)
    {
        var date = saleDate ?? await repository.LastDateAsync(cancellationToken) ?? DateOnly.FromDateTime(DateTime.Today);
        Report = await repository.GetAsync(date, storeCode.GetValueOrDefault(), selectedKey, cancellationToken);
    }

    public async Task<JsonResult> OnGetDetailsAsync(int id, int year, int code, CancellationToken cancellationToken = default) =>
        new(await repository.GetDetailsAsync(id, year, code, cancellationToken));
}
