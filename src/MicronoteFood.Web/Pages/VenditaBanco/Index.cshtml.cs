using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.VenditaBanco;

public sealed class IndexModel(
    CounterSaleRepository repository,
    ApplicationState applicationState) : PageModel
{
    public CounterSalePageData Data { get; private set; } = new();

    [BindProperty]
    public string SalePayload { get; set; } = "";

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Data = await repository.LoadAsync(applicationState.Esercizio, cancellationToken);

    public async Task<IActionResult> OnPostCloseAsync(CancellationToken cancellationToken)
    {
        CounterSaleSaveModel? sale;
        try
        {
            sale = JsonSerializer.Deserialize<CounterSaleSaveModel>(
                SalePayload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            sale = null;
        }
        if (sale is null)
        {
            TempData["ErrorMessage"] = "Dati della vendita non validi.";
            return RedirectToPage();
        }
        try
        {
            var result = await repository.SaveAsync(
                applicationState.Esercizio, sale, cancellationToken);
            TempData["SuccessMessage"] = $"Vendita {result.Code:000000} registrata.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        return RedirectToPage();
    }
}
