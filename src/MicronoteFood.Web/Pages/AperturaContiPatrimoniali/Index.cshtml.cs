using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.AperturaContiPatrimoniali;

public class IndexModel(
    OpeningBalanceRepository repository,
    ApplicationState applicationState) : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OpeningBalanceList List { get; private set; } =
        new(applicationState.Esercizio, [], new DateOnly(applicationState.Esercizio, 1, 1), []);

    [BindProperty(SupportsGet = true)]
    public int? Year { get; set; }

    [BindProperty]
    public string MovementDate { get; set; } = "";

    [BindProperty]
    public string Payload { get; set; } = "";

    [TempData]
    public string? SavedMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCalculateAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        var calculated = await repository.CalculateBalancesAsync(List.Year, cancellationToken);
        var byAccount = calculated.ToDictionary(row => row.AccountCode);
        List = List with
        {
            Rows = List.Rows
                .Select(row => byAccount.TryGetValue(row.AccountCode, out var balance)
                    ? row with { Debit = balance.Debit, Credit = balance.Credit }
                    : row with { Debit = 0, Credit = 0 })
                .ToArray()
        };
        Payload = JsonSerializer.Serialize(calculated);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var selectedYear = Year.GetValueOrDefault(applicationState.Esercizio);
        var movementDate = ParseMovementDate(selectedYear);
        var rows = ParsePayload();
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        await repository.SaveAsync(selectedYear, movementDate, rows, cancellationToken);
        SavedMessage = $"Apertura conti patrimoniali salvata correttamente per l'esercizio {selectedYear}.";
        return RedirectToPage(new { year = selectedYear });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var selectedYear = Year.GetValueOrDefault(applicationState.Esercizio);
        List = await repository.ListAsync(selectedYear, applicationState.Esercizio, cancellationToken);
        Year = List.Year;
        MovementDate = List.MovementDate.ToString("dd-MM-yyyy", CultureInfo.GetCultureInfo("it-IT"));
    }

    private DateOnly ParseMovementDate(int selectedYear)
    {
        if (DateOnly.TryParseExact(
                MovementDate,
                ["dd-MM-yyyy", "dd/MM/yyyy", "yyyy-MM-dd"],
                CultureInfo.GetCultureInfo("it-IT"),
                DateTimeStyles.None,
                out var date))
        {
            return date;
        }

        ModelState.AddModelError("", "Data movimento non valida.");
        return new DateOnly(selectedYear, 1, 1);
    }

    private IReadOnlyList<OpeningBalanceSaveRow> ParsePayload()
    {
        if (string.IsNullOrWhiteSpace(Payload))
        {
            ModelState.AddModelError("", "Nessun saldo da salvare.");
            return [];
        }

        try
        {
            var rows = JsonSerializer.Deserialize<List<OpeningBalanceSaveRow>>(
                Payload,
                JsonOptions) ?? [];
            if (rows.Count == 0)
            {
                ModelState.AddModelError("", "Nessun saldo da salvare.");
            }

            return rows;
        }
        catch (JsonException)
        {
            ModelState.AddModelError("", "Dati apertura non validi.");
            return [];
        }
    }
}
