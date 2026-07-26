using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.RiepilogoMovimentiContabili;

public sealed class IndexModel(
    AccountingMovementSummaryRepository repository,
    ApplicationState applicationState) : PageModel
{
    public AccountingMovementSummaryPageModel Report { get; private set; } = new();

    public async Task OnGetAsync(bool run, int? year, CancellationToken cancellationToken)
    {
        var selectedYear = year ?? applicationState.Esercizio;
        Report = run
            ? await repository.GetAsync(selectedYear, applicationState.Esercizio, cancellationToken)
            : await repository.GetInitialAsync(selectedYear, applicationState.Esercizio);
    }
}
