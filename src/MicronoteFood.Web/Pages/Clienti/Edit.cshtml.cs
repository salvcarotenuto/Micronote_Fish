using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.Clienti;

public class EditModel(CustomerRepository repository) : PageModel
{
    [BindProperty]
    public CustomerEditModel Customer { get; set; } = new();

    [BindProperty]
    public int Azione { get; set; } = FormAzione.Inserimento;

    public bool IsNew => FormAzione.IsInserimento(Azione);

    public int DisplayCode { get; private set; }

    public SelectList Stores { get; private set; } = EmptySelectList();
    public SelectList Categories { get; private set; } = EmptySelectList();
    public SelectList Countries { get; private set; } = EmptySelectList();
    public SelectList LegalNatures { get; private set; } = EmptySelectList();
    public SelectList Accounts { get; private set; } = EmptySelectList();
    public SelectList Agents { get; private set; } = EmptySelectList();
    public SelectList Payments { get; private set; } = EmptySelectList();
    public SelectList Banks { get; private set; } = EmptySelectList();

    public async Task<IActionResult> OnGetAsync(
        int? code,
        int? azione,
        CancellationToken cancellationToken)
    {
        Azione = ResolveAzione(azione, code.HasValue);
        if (code.HasValue)
        {
            var customer = await repository.GetAsync(code.Value, cancellationToken);
            if (customer is null)
            {
                return NotFound();
            }

            Customer = customer;
        }
        else
        {
            DisplayCode = await repository.NextCodeAsync(cancellationToken);
        }

        await LoadLookupsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        Azione = FormAzione.Normalize(Azione, FormAzione.ForRecord(Customer.Code > 0));
        Normalize();
        ValidateFiscalData();

        if (!ModelState.IsValid)
        {
            if (Customer.Code == 0)
            {
                DisplayCode = await repository.NextCodeAsync(cancellationToken);
            }

            PromoteFirstModelErrorToSummary();
            await LoadLookupsAsync(cancellationToken);
            return Page();
        }

        if (Customer.Code == 0)
        {
            await repository.InsertAsync(Customer, cancellationToken);
        }
        else
        {
            var updated = await repository.UpdateAsync(Customer, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }
        }

        return RedirectToPage("./Index");
    }

    private static int ResolveAzione(int? azione, bool hasRecord)
    {
        if (azione is > 0)
        {
            return FormAzione.Normalize(azione.Value, FormAzione.ForRecord(hasRecord));
        }

        return FormAzione.ForRecord(hasRecord);
    }

    private async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        var lookups = await repository.GetLookupsAsync(cancellationToken);
        Stores = Select(lookups.Stores);
        Categories = Select(lookups.Categories);
        Countries = Select(lookups.Countries);
        LegalNatures = Select(lookups.LegalNatures);
        Accounts = Select(lookups.Accounts);
        Agents = Select(lookups.Agents);
        Payments = Select(lookups.Payments);
        Banks = Select(lookups.Banks);
    }

    private void Normalize()
    {
        Customer.Name = Customer.Name?.Trim() ?? "";
        Customer.TaxCode = FiscalCodeService.NormalizeFiscalCode(Customer.TaxCode);
        Customer.VatNumber = FiscalCodeService.NormalizeVatNumber(Customer.VatNumber);
        Customer.City = Customer.City?.Trim();
        Customer.PostalCode = Customer.PostalCode?.Trim();
        Customer.Province = Customer.Province?.Trim().ToUpperInvariant();
        Customer.SdiCode = Customer.SdiCode?.Trim().ToUpperInvariant();
        Customer.Email = Customer.Email?.Trim();
        Customer.CertifiedEmail = Customer.CertifiedEmail?.Trim();
    }
    private void ValidateFiscalData()
    {
        if (!string.IsNullOrWhiteSpace(Customer.TaxCode)
            && !FiscalCodeService.IsValidFiscalCode(Customer.TaxCode))
        {
            ModelState.AddModelError("Customer.TaxCode", "Codice fiscale non valido.");
        }

        if (!string.IsNullOrWhiteSpace(Customer.VatNumber)
            && !FiscalCodeService.IsValidVatNumber(Customer.VatNumber))
        {
            ModelState.AddModelError("Customer.VatNumber", "Partita IVA non valida.");
        }
    }
    private void PromoteFirstModelErrorToSummary()
    {
        if (ModelState[string.Empty]?.Errors.Count > 0)
        {
            return;
        }

        var firstError = ModelState
            .SelectMany(entry => entry.Value?.Errors ?? [])
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

        if (!string.IsNullOrWhiteSpace(firstError))
        {
            ModelState.AddModelError(string.Empty, firstError);
        }
    }

    private static SelectList Select(IReadOnlyList<LookupOption> items) =>
        new(items, nameof(LookupOption.Code), nameof(LookupOption.Description));

    private static SelectList EmptySelectList() =>
        new(Array.Empty<LookupOption>());
}
