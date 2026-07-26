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
    public string Payload { get; set; } = "";

    [TempData]
    public string? SavedMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Rows = await repository.ListAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var rows = ParsePayload();
        if (!ModelState.IsValid)
        {
            Rows = await repository.ListAsync(cancellationToken);
            return Page();
        }

        await repository.SaveAsync(rows, cancellationToken);
        SavedMessage = $"Registrate {rows.Count} giacenze iniziali.";
        return RedirectToPage();
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
