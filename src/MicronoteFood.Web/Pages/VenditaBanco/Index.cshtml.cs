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
    public CounterSaleSaveModel? Draft { get; private set; }

    [BindProperty]
    public string SalePayload { get; set; } = "";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Data = await repository.LoadAsync(applicationState.Esercizio, cancellationToken);
        Draft = await repository.LoadDraftAsync(
            applicationState.Esercizio,
            CurrentUserCode(),
            cancellationToken);
    }

    public async Task<IActionResult> OnGetLastPriceAsync(
        int customerCode,
        string articleCode,
        CancellationToken cancellationToken)
    {
        var lastPrice = await repository.LoadLastPriceAsync(
            customerCode, articleCode, cancellationToken);
        return new JsonResult(new
        {
            price = lastPrice?.Price,
            vatRate = lastPrice?.VatRate
        });
    }

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
            await repository.SaveAsync(
                applicationState.Esercizio, CurrentUserCode(), sale, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveDraftAsync(CancellationToken cancellationToken)
    {
        var draft = DeserializePayload();
        if (draft is null)
            return BadRequest();
        var draftId = await repository.SaveDraftAsync(
            applicationState.Esercizio,
            CurrentUserCode(),
            draft,
            cancellationToken);
        return new JsonResult(new { saved = true, draftId });
    }

    public async Task<IActionResult> OnPostDeleteDraftAsync(CancellationToken cancellationToken)
    {
        var draft = DeserializePayload();
        await repository.DeleteDraftAsync(
            applicationState.Esercizio,
            CurrentUserCode(),
            draft?.DraftId ?? 0,
            cancellationToken);
        return new JsonResult(new { deleted = true });
    }

    private CounterSaleSaveModel? DeserializePayload()
    {
        try
        {
            return JsonSerializer.Deserialize<CounterSaleSaveModel>(
                SalePayload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private int CurrentUserCode()
    {
        var value = HttpContext.Session.GetString(ApplicationAuthService.UserCodeKey);
        return int.TryParse(value, out var code) && code > 0 ? code : 0;
    }
}
