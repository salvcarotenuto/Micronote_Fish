using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.FattureAcquisto;

public sealed class IndexModel(
    PurchaseInvoiceRepository repository,
    ApplicationState applicationState) : PageModel
{
    public PurchaseInvoiceListPageModel List { get; private set; } = new();

    public async Task OnGetAsync(
        int? year,
        int? month,
        int? causeCode,
        int? supplierCode,
        int? storeCode,
        int? contraAccountCode,
        bool showDueDates,
        int? selectedId,
        CancellationToken cancellationToken)
    {
        var selectedYear = year.GetValueOrDefault(applicationState.Esercizio);

        List = await repository.GetListAsync(
            selectedYear,
            month,
            causeCode,
            supplierCode,
            storeCode,
            contraAccountCode,
            showDueDates,
            selectedId,
            cancellationToken);
    }
    public async Task<IActionResult> OnPostDeleteAsync(
        int invoiceId,
        int? year,
        int? month,
        int? causeCode,
        int? supplierCode,
        int? storeCode,
        int? contraAccountCode,
        bool showDueDates,
        CancellationToken cancellationToken)
    {
        if (invoiceId > 0)
        {
            await repository.DeleteByIdAsync(invoiceId, cancellationToken);
        }

        return RedirectToPage("./Index", new
        {
            year,
            month,
            causeCode,
            supplierCode,
            storeCode,
            contraAccountCode,
            showDueDates
        });
    }
}
