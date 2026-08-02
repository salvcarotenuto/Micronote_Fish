using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.MovimentiCassa;

public sealed class IndexModel(
    CashMovementListRepository repository,
    ApplicationState applicationState) : PageModel
{
    public CashMovementListPageModel List { get; private set; } = new();

    public async Task OnGetAsync(
        int? year,
        string? period,
        string? movementType,
        int? paymentMethod,
        int? causeCode,
        int? storeCode,
        int? selectedId,
        CancellationToken cancellationToken)
    {
        var normalizedType = movementType is "E" or "U" ? movementType : "";
        List = await repository.GetAsync(
            year is > 0 ? year.Value : applicationState.Esercizio,
            applicationState.Esercizio,
            period?.Trim() ?? "",
            normalizedType,
            paymentMethod is >= 0 and <= 2 ? paymentMethod : null,
            causeCode is > 0 ? causeCode : null,
            storeCode is > 0 ? storeCode : null,
            selectedId,
            cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        int id,
        int year,
        string? period,
        string? movementType,
        int? paymentMethod,
        int? causeCode,
        int? storeCode,
        CancellationToken cancellationToken)
    {
        if (id > 0) await repository.DeleteAsync(id, cancellationToken);
        return RedirectToPage(new { year, period, movementType, paymentMethod, causeCode, storeCode });
    }
}
