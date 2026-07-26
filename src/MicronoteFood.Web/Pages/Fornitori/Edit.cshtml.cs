using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.Fornitori;

public class EditModel(SupplierRepository repository) : PageModel
{
    [BindProperty]
    public SupplierEditModel Supplier { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public int Azione { get; set; } = FormAzione.Inserimento;

    public bool IsNew => FormAzione.IsInserimento(Azione);

    public int DisplayCode { get; private set; }

    public string BackUrl => Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Page("./Index") ?? "/Fornitori";

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
        string? name,
        string? vat,
        string? fiscalCode,
        string? address,
        string? city,
        string? postalCode,
        string? province,
        string? phone,
        string? email,
        string? certifiedEmail,
        CancellationToken cancellationToken)
    {
        Azione = ResolveAzione(azione, code.HasValue);
        if (code.HasValue)
        {
            var supplier = await repository.GetAsync(code.Value, cancellationToken);
            if (supplier is null)
            {
                return NotFound();
            }

            Supplier = supplier;
        }
        else
        {
            DisplayCode = await repository.NextCodeAsync(cancellationToken);
            Supplier.Name = name?.Trim() ?? "";
            Supplier.VatNumber = FiscalCodeService.NormalizeVatNumber(vat);
            Supplier.TaxCode = FiscalCodeService.NormalizeFiscalCode(fiscalCode);
            Supplier.Address = address?.Trim();
            Supplier.City = city?.Trim();
            Supplier.PostalCode = postalCode?.Trim();
            Supplier.Province = province?.Trim().ToUpperInvariant();
            Supplier.Phone = phone?.Trim();
            Supplier.Email = email?.Trim();
            Supplier.CertifiedEmail = certifiedEmail?.Trim();
        }

        await LoadLookupsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        Azione = FormAzione.Normalize(Azione, FormAzione.ForRecord(Supplier.Code > 0));
        Normalize();
        ValidateFiscalData();

        if (!ModelState.IsValid)
        {
            if (Supplier.Code == 0)
            {
                DisplayCode = await repository.NextCodeAsync(cancellationToken);
            }

            PromoteFirstModelErrorToSummary();
            await LoadLookupsAsync(cancellationToken);
            return Page();
        }

        if (Supplier.Code == 0)
        {
            await repository.InsertAsync(Supplier, cancellationToken);
        }
        else
        {
            var updated = await repository.UpdateAsync(Supplier, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }
        }

        return LocalRedirect(BackUrl);
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
        Supplier.Name = Supplier.Name?.Trim() ?? "";
        Supplier.TaxCode = FiscalCodeService.NormalizeFiscalCode(Supplier.TaxCode);
        Supplier.VatNumber = FiscalCodeService.NormalizeVatNumber(Supplier.VatNumber);
        Supplier.City = Supplier.City?.Trim();
        Supplier.PostalCode = Supplier.PostalCode?.Trim();
        Supplier.Province = Supplier.Province?.Trim().ToUpperInvariant();
        Supplier.SdiCode = Supplier.SdiCode?.Trim().ToUpperInvariant();
        Supplier.Email = Supplier.Email?.Trim();
        Supplier.CertifiedEmail = Supplier.CertifiedEmail?.Trim();
    }
    private void ValidateFiscalData()
    {
        if (!string.IsNullOrWhiteSpace(Supplier.TaxCode)
            && !FiscalCodeService.IsValidFiscalCode(Supplier.TaxCode))
        {
            ModelState.AddModelError("Supplier.TaxCode", "Codice fiscale non valido.");
        }

        if (!string.IsNullOrWhiteSpace(Supplier.VatNumber)
            && !FiscalCodeService.IsValidVatNumber(Supplier.VatNumber))
        {
            ModelState.AddModelError("Supplier.VatNumber", "Partita IVA non valida.");
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
