using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.SaldiClientiFornitori;

public sealed class IndexModel(
    CustomerSupplierBalanceSummaryRepository repository,
    ApplicationState applicationState) : PageModel
{
    public CustomerSupplierBalanceSummaryPageModel Report { get; private set; } = new();

    public async Task OnGetAsync(
        int? year,
        bool showZero,
        int? storeCode,
        CancellationToken cancellationToken)
    {
        Report = await repository.GetAsync(
            year ?? applicationState.Esercizio,
            applicationState.Esercizio,
            showZero,
            storeCode,
            cancellationToken);
    }
}
