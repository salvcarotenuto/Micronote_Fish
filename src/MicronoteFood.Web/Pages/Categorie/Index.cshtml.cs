using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.Categorie;

public class IndexModel(CategoryRepository repository) : PageModel
{
    public IReadOnlyList<CategoryListItem> Categories { get; private set; } = [];

    public int DisplayCode { get; private set; }

    [BindProperty]
    public CategoryEditModel Category { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? Selected { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Categories = await repository.ListAsync(cancellationToken);
        DisplayCode = await repository.NextCodeAsync(cancellationToken);
        Category = Selected.HasValue
            ? await repository.GetAsync(Selected.Value, cancellationToken) ?? new()
            : FirstCategory();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Normalize();

        if (!ModelState.IsValid)
        {
            Categories = await repository.ListAsync(cancellationToken);
            DisplayCode = await repository.NextCodeAsync(cancellationToken);
            return Page();
        }

        if (Category.Code == 0)
        {
            await repository.InsertAsync(Category, cancellationToken);
        }
        else
        {
            var updated = await repository.UpdateAsync(Category, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }
        }

        return RedirectToPage(new { selected = Category.Code });
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

    private CategoryEditModel FirstCategory()
    {
        var first = Categories.FirstOrDefault();
        return first is null
            ? new CategoryEditModel()
            : new CategoryEditModel
            {
                Code = first.Code,
                Description = first.Description
            };
    }

    private void Normalize()
    {
        Category.Description = ToItalianTitleCase(Category.Description);
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
