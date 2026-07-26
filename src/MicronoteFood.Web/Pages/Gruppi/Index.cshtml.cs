using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.Gruppi;

public class IndexModel(GroupRepository repository) : PageModel
{
    public IReadOnlyList<GroupListItem> Groups { get; private set; } = [];

    public int DisplayCode { get; private set; }

    [BindProperty]
    public GroupEditModel Group { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? Selected { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Groups = await repository.ListAsync(cancellationToken);
        DisplayCode = await repository.NextCodeAsync(cancellationToken);
        Group = Selected.HasValue
            ? await repository.GetAsync(Selected.Value, cancellationToken) ?? new()
            : FirstGroup();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Normalize();

        if (!ModelState.IsValid)
        {
            Groups = await repository.ListAsync(cancellationToken);
            DisplayCode = await repository.NextCodeAsync(cancellationToken);
            return Page();
        }

        if (Group.Code == 0)
        {
            await repository.InsertAsync(Group, cancellationToken);
        }
        else
        {
            var updated = await repository.UpdateAsync(Group, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }
        }

        return RedirectToPage(new { selected = Group.Code });
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

    private GroupEditModel FirstGroup()
    {
        var first = Groups.FirstOrDefault();
        return first is null
            ? new GroupEditModel()
            : new GroupEditModel
            {
                Code = first.Code,
                Description = first.Description
            };
    }

    private void Normalize()
    {
        Group.Description = ToItalianTitleCase(Group.Description);
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
