using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.GiacenzaInizialeArticoli;

public class IndexModel(InitialArticleStockRepository repository) : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<InitialArticleStockRow> Rows { get; private set; } = [];

    [BindProperty]
    public DateOnly? InventoryDate { get; set; }

    [BindProperty]
    public string Payload { get; set; } = "";

    [TempData]
    public string? SavedMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var rows = ParsePayload();
        if (InventoryDate is null)
        {
            ModelState.AddModelError(nameof(InventoryDate), "Indicare la data di effettuazione dell'inventario.");
        }

        if (!ModelState.IsValid)
        {
            var data = await repository.GetAsync(cancellationToken);
            Rows = data.Rows;
            return Page();
        }

        var inventoryDate = InventoryDate.GetValueOrDefault();
        await repository.SaveAsync(inventoryDate, rows, cancellationToken);
        SavedMessage = $"Registrato l'inventario del {inventoryDate:dd/MM/yyyy} per {rows.Count} articoli.";
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var data = await repository.GetAsync(cancellationToken);
        Rows = data.Rows;
        InventoryDate = data.InventoryDate;
    }

    private IReadOnlyList<InitialArticleStockSaveRow> ParsePayload()
    {
        if (string.IsNullOrWhiteSpace(Payload))
        {
            ModelState.AddModelError("", "Nessuna giacenza da registrare.");
            return [];
        }

        try
        {
            var rows = JsonSerializer.Deserialize<List<InitialArticleStockSaveRow>>(Payload, JsonOptions) ?? [];
            if (rows.Count == 0)
            {
                ModelState.AddModelError("", "Nessuna giacenza da registrare.");
            }

            return rows;
        }
        catch (JsonException)
        {
            ModelState.AddModelError("", "Dati giacenze non validi.");
            return [];
        }
    }
}
