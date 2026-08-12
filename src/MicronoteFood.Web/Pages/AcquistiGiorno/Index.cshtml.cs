using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.AcquistiGiorno;

public sealed class IndexModel(
    DailyPurchasesRepository repository,
    ApplicationState applicationState) : PageModel
{
    public DailyPurchasesPageModel Report { get; private set; } = new();
    public SupplierBalanceModel? SupplierBalance { get; private set; }

    public async Task OnGetAsync(
        DateOnly? purchaseDate,
        string? selectedKey,
        CancellationToken cancellationToken = default)
    {
        var date = purchaseDate
            ?? await repository.LastDateAsync(cancellationToken)
            ?? DateOnly.FromDateTime(DateTime.Today);
        Report = await repository.GetAsync(date, selectedKey, cancellationToken);

        var selected = Report.Purchases.FirstOrDefault(row => row.Key == Report.SelectedKey);
        if (selected?.SupplierCode > 0)
        {
            var year = applicationState.Esercizio;
            SupplierBalance = await repository.GetSupplierBalanceAsync(
                selected.SupplierCode,
                year,
                cancellationToken);
        }
    }

    public async Task<JsonResult> OnGetDetailsAsync(
        int id,
        int year,
        int code,
        CancellationToken cancellationToken = default)
    {
        var details = await repository.GetDetailsAsync(id, year, code, cancellationToken);
        return new JsonResult(details);
    }
}
