using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.Settori;

public class IndexModel(SectorRepository repository) : PageModel
{
    public IReadOnlyList<SectorListItem> Sectors { get; private set; } = [];

    public int DisplayCode { get; private set; }

    [BindProperty]
    public SectorEditModel Sector { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? Selected { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Sectors = await repository.SearchAsync(null, cancellationToken);
        DisplayCode = await repository.NextCodeAsync(cancellationToken);
        Sector = Selected.HasValue
            ? await repository.GetAsync(Selected.Value, cancellationToken) ?? new()
            : FirstSector();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Normalize();

        if (!ModelState.IsValid)
        {
            Sectors = await repository.SearchAsync(null, cancellationToken);
            DisplayCode = await repository.NextCodeAsync(cancellationToken);
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

        return RedirectToPage(new { selected = Sector.Code });
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        int code,
        CancellationToken cancellationToken)
    {
        var result = await repository.DeleteAsync(code, cancellationToken);
        if (!result.Deleted)
        {
            ErrorMessage = result.Message;
        }

        return RedirectToPage();
    }

    private SectorEditModel FirstSector()
    {
        var first = Sectors.FirstOrDefault();
        return first is null
            ? new SectorEditModel()
            : new SectorEditModel
            {
                Code = first.Code,
                Description = first.Description
            };
    }

    private void Normalize()
    {
        Sector.Description = Sector.Description?.Trim() ?? "";
    }
}
