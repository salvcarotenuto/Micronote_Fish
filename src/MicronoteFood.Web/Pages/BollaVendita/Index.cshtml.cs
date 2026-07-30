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
    public string ReturnUrl { get; private set; } = "/";
    public bool ReturnToHistory { get; private set; }

    [BindProperty]
    public string? SavePayload { get; set; }

    public async Task OnGetAsync(
        int? id,
        string? returnTo,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        ReturnToHistory = string.Equals(returnTo, "history", StringComparison.OrdinalIgnoreCase);
        ReturnUrl = ReturnToHistory
            ? NormalizeReturnUrl(returnUrl) ?? "/Vendite/Storico"
            : "/";
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
        var form = await Request.ReadFormAsync(cancellationToken);
        var payload = form["SavePayload"].ToString();
        if (string.IsNullOrWhiteSpace(payload))
        {
            return BadRequest("Dati della vendita mancanti.");
        }

        var model = JsonSerializer.Deserialize<SalesDocumentSaveModel>(
            payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Dati della vendita mancanti.");
        await repository.SaveAsync(applicationState.Esercizio, model, cancellationToken);

        var returnToHistory = string.Equals(
            Request.Query["returnTo"],
            "history",
            StringComparison.OrdinalIgnoreCase);
        if (returnToHistory)
        {
            return LocalRedirect(
                NormalizeReturnUrl(Request.Query["returnUrl"]) ?? "/Vendite/Storico");
        }

        return RedirectToPage("/BollaVendita/Index");
    }

    private static string? NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        return returnUrl.StartsWith('/')
            && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
            ? returnUrl
            : null;
    }
}
