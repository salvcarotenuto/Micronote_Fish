using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.VenditePerGruppo;

public sealed class IndexModel(GroupSalesStatsRepository repository, ApplicationState applicationState) : PageModel
{
    public GroupSalesStatsPageModel Report { get; private set; } = new();
    public async Task OnGetAsync(bool run, DateOnly? dateFrom, DateOnly? dateTo, string? grouping, CancellationToken cancellationToken)
    {
        var year = applicationState.Esercizio; var start = dateFrom ?? new DateOnly(year, 1, 1); var end = dateTo ?? DateOnly.FromDateTime(DateTime.Today);
        if (end.Year != year) end = new DateOnly(year, 12, 31);
        Report = await repository.GetAsync(start, end, grouping, run, cancellationToken);
    }
    public async Task<JsonResult> OnGetDetailsAsync(int code, DateOnly dateFrom, DateOnly dateTo, string? grouping, CancellationToken cancellationToken)
    {
        var details = await repository.GetDetailsAsync(code, dateFrom, dateTo, grouping, cancellationToken);
        return new JsonResult(new { articles = details.Articles.Select(row => new { code = row.Code, description = row.Description, amount = row.Amount }), suppliers = details.Customers.Select(row => new { code = row.Code, name = row.Name, amount = row.Amount }) });
    }
}
