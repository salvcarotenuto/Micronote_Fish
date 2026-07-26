using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.EstrattoContoClientiFornitori;

public sealed class IndexModel(
    AccountingStatementRepository repository,
    ApplicationState applicationState) : PageModel
{
    public AccountingStatementPageModel Report { get; private set; } = new();

    public async Task OnGetAsync(
        string? partyType,
        int? partyCode,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        CancellationToken cancellationToken)
    {
        var year = applicationState.Esercizio;
        var from = dateFrom ?? new DateOnly(year, 1, 1);
        var to = dateTo ?? DefaultDateTo(year);

        Report = !string.IsNullOrWhiteSpace(partyType) && partyCode is > 0
            ? await repository.GetPartyAsync(partyType, partyCode.Value, from, to, cancellationToken)
            : await repository.GetPartyInitialAsync(year, cancellationToken);
    }

    private static DateOnly DefaultDateTo(int year)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return today.Year == year ? today : new DateOnly(year, 12, 31);
    }
}
