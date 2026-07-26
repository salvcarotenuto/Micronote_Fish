using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.Settori;

public class EditModel(SectorRepository repository) : PageModel
{
    [BindProperty]
    public SectorEditModel Sector { get; set; } = new();

    [BindProperty]
    public int Azione { get; set; } = FormAzione.Inserimento;

    public bool IsNew => FormAzione.IsInserimento(Azione);

    public int DisplayCode { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        int? code,
        int? azione,
        CancellationToken cancellationToken)
    {
        Azione = ResolveAzione(azione, code.HasValue);
        if (code.HasValue)
        {
            var sector = await repository.GetAsync(code.Value, cancellationToken);
            if (sector is null)
            {
                return NotFound();
            }

            Sector = sector;
        }
        else
        {
            DisplayCode = await repository.NextCodeAsync(cancellationToken);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Azione = FormAzione.Normalize(Azione, FormAzione.ForRecord(Sector.Code > 0));
        Normalize();

        if (!ModelState.IsValid)
        {
            if (Sector.Code == 0)
            {
                DisplayCode = await repository.NextCodeAsync(cancellationToken);
            }

            return Page();
        }

        if (Sector.Code == 0)
        {
            await repository.InsertAsync(Sector, cancellationToken);
        }
        else
        {
            var updated = await repository.UpdateAsync(Sector, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }
        }

        return RedirectToPage("/Settori/Index");
    }

    private static int ResolveAzione(int? azione, bool hasRecord)
    {
        if (azione is > 0)
        {
            return FormAzione.Normalize(azione.Value, FormAzione.ForRecord(hasRecord));
        }

        return FormAzione.ForRecord(hasRecord);
    }

    private void Normalize()
    {
        Sector.Description = Sector.Description?.Trim() ?? "";
    }
}
