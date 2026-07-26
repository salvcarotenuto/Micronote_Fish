using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.Sottogruppi;

public class IndexModel(SubgroupRepository repository) : PageModel
{
    public IReadOnlyList<SubgroupListItem> Subgroups { get; private set; } = [];

    public int DisplayCode { get; private set; }

    [BindProperty]
    public SubgroupEditModel Subgroup { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? Selected { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Subgroups = await repository.ListAsync(cancellationToken);
        DisplayCode = await repository.NextCodeAsync(cancellationToken);
        Subgroup = Selected.HasValue
            ? await repository.GetAsync(Selected.Value, cancellationToken) ?? new()
            : FirstSubgroup();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Normalize();

        if (!ModelState.IsValid)
        {
            Subgroups = await repository.ListAsync(cancellationToken);
            DisplayCode = await repository.NextCodeAsync(cancellationToken);
            return Page();
        }

        if (Subgroup.Code == 0)
        {
            await repository.InsertAsync(Subgroup, cancellationToken);
        }
        else
        {
            var updated = await repository.UpdateAsync(Subgroup, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }
        }

        return RedirectToPage(new { selected = Subgroup.Code });
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

    private SubgroupEditModel FirstSubgroup()
    {
        var first = Subgroups.FirstOrDefault();
        return first is null
            ? new SubgroupEditModel()
            : new SubgroupEditModel
            {
                Code = first.Code,
                Description = first.Description
            };
    }

    private void Normalize()
    {
        Subgroup.Description = ToItalianTitleCase(Subgroup.Description);
    }

    private static string ToItalianTitleCase(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        var culture = CultureInfo.GetCultureInfo("it-IT");
        return culture.TextInfo.ToTitleCase(trimmed.ToLower(culture));
    }
}
