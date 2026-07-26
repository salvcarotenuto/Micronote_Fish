using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.BilancioVerifica;

public sealed class IndexModel(
    TrialBalanceRepository repository,
    ApplicationState applicationState,
    TrialBalancePdfService pdfService) : PageModel
{
    public TrialBalancePageModel Report { get; private set; } = new();

    public async Task OnGetAsync(
        bool run,
        int? year,
        CancellationToken cancellationToken)
    {
        var selectedYear = year ?? applicationState.Esercizio;
        Report = run
            ? await repository.GetAsync(selectedYear, applicationState.Esercizio, null, cancellationToken)
            : await repository.GetInitialAsync(selectedYear, applicationState.Esercizio, null, cancellationToken);
    }

    public async Task<IActionResult> OnPostPdfAsync(
        [FromBody] TrialBalancePrintRequest request,
        CancellationToken cancellationToken)
    {
        var report = await repository.GetAsync(
            request.Year, applicationState.Esercizio, null, cancellationToken);
        var now = DateTime.Now;
        var pdf = pdfService.Create(report, now);
        return File(pdf, "application/pdf", $"Bilancio_{request.Year}_{now:yyyyMMdd_HHmm}.pdf");
    }
}
