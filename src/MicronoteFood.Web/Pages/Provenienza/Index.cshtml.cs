using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.Provenienza;

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
        Values = await repository.ListOriginsAsync(cancellationToken);
        DisplayCode = await repository.NextOriginCodeAsync(cancellationToken);
        Value = Selected.HasValue
            ? await repository.GetOriginAsync(Selected.Value, cancellationToken) ?? new()
            : FirstValue();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Value.Description = Value.Description?.Trim() ?? "";
        if (!ModelState.IsValid)
        {
            Values = await repository.ListOriginsAsync(cancellationToken);
            DisplayCode = await repository.NextOriginCodeAsync(cancellationToken);
            return Page();
        }

        if (Value.Code == 0)
        {
            await repository.InsertOriginAsync(Value, cancellationToken);
        }
        else if (!await repository.UpdateOriginAsync(Value, cancellationToken))
        {
            return NotFound();
        }

        return RedirectToPage(new { selected = Value.Code });
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        int code,
        CancellationToken cancellationToken)
    {
        var result = await repository.DeleteOriginAsync(code, cancellationToken);
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
