using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.Specie;

public class IndexModel(FishClassificationRepository repository) : PageModel
{
    public IReadOnlyList<FishClassificationListItem> Values { get; private set; } = [];
    public int DisplayCode { get; private set; }

    [BindProperty]
    public FishClassificationEditModel Value { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? Selected { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Values = await repository.ListSpeciesAsync(cancellationToken);
        DisplayCode = await repository.NextSpeciesCodeAsync(cancellationToken);
        Value = Selected.HasValue
            ? await repository.GetSpeciesAsync(Selected.Value, cancellationToken) ?? new()
            : FirstValue();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Value.Description = Value.Description?.Trim() ?? "";
        if (!ModelState.IsValid)
        {
            Values = await repository.ListSpeciesAsync(cancellationToken);
            DisplayCode = await repository.NextSpeciesCodeAsync(cancellationToken);
            return Page();
        }

        if (Value.Code == 0)
        {
            await repository.InsertSpeciesAsync(Value, cancellationToken);
        }
        else if (!await repository.UpdateSpeciesAsync(Value, cancellationToken))
        {
            return NotFound();
        }

        return RedirectToPage(new { selected = Value.Code });
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        int code,
        CancellationToken cancellationToken)
    {
        var result = await repository.DeleteSpeciesAsync(code, cancellationToken);
        if (!result.Deleted)
        {
            ErrorMessage = result.Message;
        }

        return RedirectToPage();
    }

    private FishClassificationEditModel FirstValue()
    {
        var first = Values.FirstOrDefault();
        return first is null
            ? new()
            : new() { Code = first.Code, Description = first.Description };
    }
}
