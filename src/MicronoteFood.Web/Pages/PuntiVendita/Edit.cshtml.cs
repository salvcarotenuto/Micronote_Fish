using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.PuntiVendita;

public class EditModel(StoreRepository repository) : PageModel
{
    [BindProperty]
    public StoreEditModel Store { get; set; } = new();

    [BindProperty]
    public int Azione { get; set; } = FormAzione.Inserimento;

    public bool IsNew => FormAzione.IsInserimento(Azione);

    public int DisplayCode { get; private set; }

    public SelectList Sectors { get; private set; } = EmptySelectList();

    public async Task<IActionResult> OnGetAsync(
        int? code,
        int? azione,
        CancellationToken cancellationToken)
    {
        Azione = ResolveAzione(azione, code.HasValue);
        if (code.HasValue)
        {
            var store = await repository.GetAsync(code.Value, cancellationToken);
            if (store is null)
            {
                return NotFound();
            }

            Store = store;
        }
        else
        {
            DisplayCode = await repository.NextCodeAsync(cancellationToken);
        }

        await LoadLookupsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Azione = FormAzione.Normalize(Azione, FormAzione.ForRecord(Store.Code > 0));
        Normalize();

        if (!ModelState.IsValid)
        {
            if (Store.Code == 0)
            {
                DisplayCode = await repository.NextCodeAsync(cancellationToken);
            }

            await LoadLookupsAsync(cancellationToken);
            return Page();
        }

        if (Store.Code == 0)
        {
            await repository.InsertAsync(Store, cancellationToken);
        }
        else
        {
            var updated = await repository.UpdateAsync(Store, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }
        }

        return RedirectToPage("/PuntiVendita/Index");
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
        Sectors = new SelectList(
            lookups.Sectors,
            nameof(LookupOption.Code),
            nameof(LookupOption.Description));
    }

    private void Normalize()
    {
        Store.Name = Store.Name.Trim();
        Store.Description = Store.Description?.Trim();
        Store.City = Store.City?.Trim();
        Store.PostalCode = Store.PostalCode?.Trim();
        Store.Address = Store.Address?.Trim();
        Store.Province = Store.Province?.Trim().ToUpperInvariant();
        Store.Contact = Store.Contact?.Trim();
        Store.Phone = Store.Phone?.Trim();
        Store.Mobile = Store.Mobile?.Trim();
        Store.Email = Store.Email?.Trim();
    }

    private static SelectList EmptySelectList() =>
        new(Array.Empty<LookupOption>());
}
