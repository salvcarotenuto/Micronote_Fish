using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.ScaricoPerditeResi;

public sealed class IndexModel(StockUnloadRepository repository, ApplicationState state) : PageModel
{
    public StockUnloadListModel List { get; private set; } = new();

    public async Task OnGetAsync(int? year, int? month, int? causeCode, int? storeCode, string? selectedKey, CancellationToken ct) =>
        List = await repository.GetListAsync(year ?? state.Esercizio, month, causeCode, storeCode, selectedKey, ct);

    public async Task<IActionResult> OnPostDeleteAsync(int id, int year, CancellationToken ct)
    {
        await repository.DeleteAsync(id, ct);
        return RedirectToPage(new { year });
    }
}
