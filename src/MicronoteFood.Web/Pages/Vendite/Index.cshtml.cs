using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.Vendite;

public class IndexModel(
    SalesEntryRepository repository,
    ApplicationState applicationState) : PageModel
{
    public SalesEntryPageModel Entry { get; private set; } = new();

    [BindProperty]
    public SalesEntrySaveModel Sale { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public string? DuplicateDateMessage { get; private set; }

    public int Azione { get; private set; } = FormAzione.Inserimento;

    public bool IsReadonly => FormAzione.IsReadonly(Azione);

    public bool IsModal => FormAzione.IsModale(Azione);

    public async Task<IActionResult> OnGetAsync(
        int? saleId,
        int? azione,
        string? returnTo,
        CancellationToken cancellationToken)
    {
        Azione = FormAzione.Normalize(
            azione ?? FormAzione.ForRecord(saleId.HasValue),
            FormAzione.ForRecord(saleId.HasValue));

        if (saleId.HasValue)
        {
            var entry = await repository.GetByIdAsync(saleId.Value, cancellationToken);
            if (entry is null)
            {
                return NotFound();
            }
            Entry = entry;
        }
        else
        {
            Entry = await repository.GetNewAsync(applicationState.Esercizio, cancellationToken);
        }

        Entry.ReturnTo = SafeReturnTo(returnTo);
        Sale = NewSaveModelFromEntry(Entry);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Entry = await repository.GetNewAsync(applicationState.Esercizio, cancellationToken);

        var command = BuildSaveCommand();
        var validationMessage = Validate(command);
        if (validationMessage is not null)
        {
            ErrorMessage = validationMessage;
            Entry = EntryFromSale();
            return Page();
        }

        var result = await repository.SaveSalesOnlyAsync(
            command,
            Sale.ConfirmOverwrite,
            cancellationToken);

        if (result.RequiresOverwriteConfirmation)
        {
            DuplicateDateMessage =
                $"Esiste già una registrazione vendite per il {Sale.MovementDate:dd/MM/yyyy}. Sovrascriverla?";
            Sale.Code = result.Code;
            Entry = EntryFromSale();
            return Page();
        }

        return RedirectAfterSave();
    }

    private SalesEntrySaveCommand BuildSaveCommand()
    {
        var rows = Sale.Rows
            .Select(row =>
            {
                var taxable = Money(row.Taxable);
                var exempt = Money(row.Exempt);
                var card = Money(row.Card);
                var tickets = Money(row.Tickets);
                var checks = Money(row.Checks);
                // Come nel legacy, la cassa transitoria coincide sempre con il non imponibile.
                var other = exempt;
                var suspended = Money(row.Suspended);
                var losses = Money(row.Losses);
                var netTaxable = taxable / (1 + Sale.SalesVatRate / 100);
                var vat = taxable - netTaxable;
                var net = netTaxable + exempt;
                var total = taxable + exempt;
                var cash = Math.Max(0, total - card - tickets - checks - other - suspended - losses);

                return new SalesEntrySaveCommandRow(
                    row.StoreCode,
                    taxable,
                    exempt,
                    Math.Round(net, 2, MidpointRounding.AwayFromZero),
                    Math.Round(vat, 2, MidpointRounding.AwayFromZero),
                    Math.Round(total, 2, MidpointRounding.AwayFromZero),
                    Math.Round(cash, 2, MidpointRounding.AwayFromZero),
                    card,
                    tickets,
                    checks,
                    other,
                    suspended,
                    losses);
            })
            .ToList();

        return new SalesEntrySaveCommand(
            Sale.SaleId,
            Sale.Year,
            Sale.Code,
            Sale.MovementDate,
            Sale.SalesVatRate,
            rows);
    }

    private string? Validate(SalesEntrySaveCommand command)
    {
        if (command.MovementDate.Year != applicationState.Esercizio)
        {
            return "Data movimento non coerente con l'esercizio contabile.";
        }

        var total = command.Rows.Sum(row => row.Total);
        if (total <= 0)
        {
            return "Totale vendita obbligatorio.";
        }

        var balance = command.Rows.Sum(row =>
            row.Total
            - row.Cash
            - row.Card
            - row.Tickets
            - row.Checks
            - row.Other
            - row.Suspended
            - row.Losses);

        return Math.Abs(balance) > 0.01m
            ? "La registrazione non quadra."
            : null;
    }

    private SalesEntryPageModel EntryFromSale()
    {
        return new SalesEntryPageModel
        {
            Year = Sale.Year,
            SaleId = Sale.SaleId,
            ReturnTo = Sale.ReturnTo,
            Code = Sale.Code,
            MovementDate = Sale.MovementDate,
            LastRegistrationDate = Entry.LastRegistrationDate,
            SalesVatRate = Sale.SalesVatRate,
            Stores = Sale.Rows
                .Select(row => new SalesEntryStoreRow(row.StoreCode, row.StoreName))
                .ToList()
        };
    }

    private static SalesEntrySaveModel NewSaveModelFromEntry(SalesEntryPageModel entry) =>
        new()
        {
            Year = entry.Year,
            SaleId = entry.SaleId,
            ReturnTo = entry.ReturnTo,
            Code = entry.Code,
            MovementDate = entry.MovementDate,
            SalesVatRate = entry.SalesVatRate,
            Rows = entry.SavedRows.Count > 0
                ? entry.SavedRows.ToList()
                : entry.Stores
                .Select(store => new SalesEntrySaveRow
                {
                    StoreCode = store.Code,
                    StoreName = store.Name
                })
                .ToList()
        };

    private static decimal Money(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return 0;
        }

        var commaIndex = normalized.LastIndexOf(',');
        var dotIndex = normalized.LastIndexOf('.');
        var decimalIndex = Math.Max(commaIndex, dotIndex);
        normalized = decimalIndex >= 0
            ? string.Concat(
                normalized[..decimalIndex].Replace(".", "").Replace(",", ""),
                ".",
                normalized[(decimalIndex + 1)..].Replace(".", "").Replace(",", ""))
            : normalized.Replace(".", "").Replace(",", "");

        return decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : 0;
    }

    private IActionResult RedirectAfterSave()
    {
        var returnTo = SafeReturnTo(Sale.ReturnTo);
        return string.IsNullOrWhiteSpace(returnTo)
            ? RedirectToPage()
            : LocalRedirect(returnTo);
    }

    private static string? SafeReturnTo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.StartsWith('/') && !value.StartsWith("//")
            ? value
            : null;
    }
}
