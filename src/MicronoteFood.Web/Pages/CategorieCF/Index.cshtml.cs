using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.CategorieCF;

public class IndexModel(CustomerSupplierCategoryRepository repository) : PageModel
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
        Category.Description = ToItalianTitleCase(Category.Description);
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
        else if (!await repository.UpdateAsync(Category, cancellationToken))
        {
            return NotFound();
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

    private static string ToItalianTitleCase(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        var culture = CultureInfo.GetCultureInfo("it-IT");
        return trimmed.Length == 0
            ? trimmed
            : culture.TextInfo.ToTitleCase(trimmed.ToLower(culture));
    }
}
