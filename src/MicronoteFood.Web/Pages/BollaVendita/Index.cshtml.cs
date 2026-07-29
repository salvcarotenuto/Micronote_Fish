using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.BollaVendita;

public sealed class IndexModel(
    SalesDocumentRepository repository,
    CounterSaleRepository counterSaleRepository,
    ApplicationState applicationState) : PageModel
{
    public SalesDocumentPageData Document { get; private set; } = new();
    public CounterSalePageData Catalogue { get; private set; } = new();
    public IReadOnlyList<SalesEntryStoreRow> Stores { get; private set; } = [];

    [BindProperty]
    public string SavePayload { get; set; } = "";

    public async Task OnGetAsync(int? id, CancellationToken cancellationToken)
    {
        Document = await repository.LoadAsync(applicationState.Esercizio, id, cancellationToken);
        Catalogue = await counterSaleRepository.LoadAsync(applicationState.Esercizio, cancellationToken);
        Stores = await repository.ListStoresAsync(cancellationToken);
    }

    public async Task<IActionResult> OnGetLastPriceAsync(
        int customerCode,
        string articleCode,
        CancellationToken cancellationToken)
    {
        var lastPrice = await counterSaleRepository.LoadLastPriceAsync(
            customerCode, articleCode, cancellationToken);
        return new JsonResult(new
        {
            price = lastPrice?.Price,
            vatRate = lastPrice?.VatRate
        });
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var model = JsonSerializer.Deserialize<SalesDocumentSaveModel>(
            SavePayload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Dati della vendita mancanti.");
        await repository.SaveAsync(applicationState.Esercizio, model, cancellationToken);
        return RedirectToPage("/Vendite/Storico");
    }
}
