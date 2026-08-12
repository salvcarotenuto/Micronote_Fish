using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.StatisticaMagazzino;

public sealed class IndexModel(WarehouseStatisticsRepository repository, ApplicationState applicationState) : PageModel
{
    public WarehouseStatisticsPageModel Report { get; private set; } = new();
    public IReadOnlyList<int> Years { get; private set; } = [];

    public async Task OnGetAsync(bool run, int? year, int? month, string? grouping, CancellationToken cancellationToken)
    {
        Years = Enumerable.Range(applicationState.Esercizio - 4, 5).Reverse().ToArray();
        var selectedYear = year.HasValue && Years.Contains(year.Value) ? year.Value : applicationState.Esercizio;
        Report = await repository.GetAsync(selectedYear, month is >= 1 and <= 12 ? month.Value : 0, grouping, run, cancellationToken);
    }

    public async Task<JsonResult> OnGetCustomersAsync(string code, int year, int month, string? grouping, CancellationToken cancellationToken)
    {
        var rows = await repository.GetCustomersAsync(code, year, month, grouping, cancellationToken);
        return new JsonResult(rows.Select(row => new { code = row.Code, name = row.Name, quantity = row.Quantity, amount = row.Amount }));
    }
}
