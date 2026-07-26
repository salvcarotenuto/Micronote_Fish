using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.EstrattoContoArticolo;

public sealed class IndexModel(
    ArticleAccountRepository repository,
    ApplicationState applicationState) : PageModel
{
    public ArticleAccountPageModel Report { get; private set; } = new();

    public async Task OnGetAsync(
        int? year,
        int? month,
        string? articleCode,
        string? selectedKey,
        CancellationToken cancellationToken)
    {
        Report = await repository.GetAsync(
            year ?? applicationState.Esercizio,
            month ?? 0,
            articleCode,
            selectedKey,
            cancellationToken);
    }
}
