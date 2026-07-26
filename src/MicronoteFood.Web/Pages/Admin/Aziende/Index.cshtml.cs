using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.Admin.Aziende;

public sealed class IndexModel(
    SystemAdminAuthService auth,
    MasterRepository repository) : PageModel
{
    public IReadOnlyList<CompanyMasterRecord> Companies { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Selected { get; set; }

    public string? Message { get; private set; }

    public bool MessageSucceeded { get; private set; } = true;

    public async Task<IActionResult> OnGetAsync(string? message, bool? success, CancellationToken cancellationToken)
    {
        if (!auth.IsLoggedIn())
        {
            return RedirectToPage("/Admin/Login");
        }

        Message = message;
        MessageSucceeded = success ?? true;
        await LoadAsync(cancellationToken);
        return Page();
    }


    public async Task<IActionResult> OnPostCreateDatabaseAsync(int code, CancellationToken cancellationToken)
    {
        if (!auth.IsLoggedIn())
        {
            return RedirectToPage("/Admin/Login");
        }

        try
        {
            var result = await repository.CreateCompanyDatabaseAsync(code, cancellationToken);
            return RedirectToPage(new { selected = code, message = result.Message, success = result.Success });
        }
        catch (Exception exception)
        {
            return RedirectToPage(new { selected = code, message = $"Creazione database non riuscita: {exception.Message}", success = false });
        }
    }

    public async Task<IActionResult> OnPostDeleteDatabaseAsync(int code, CancellationToken cancellationToken)
    {
        if (!auth.IsLoggedIn())
        {
            return RedirectToPage("/Admin/Login");
        }

        try
        {
            var result = await repository.DeleteCompanyDatabaseAsync(code, cancellationToken);
            return RedirectToPage(new { selected = code, message = result.Message, success = result.Success });
        }
        catch (Exception exception)
        {
            return RedirectToPage(new { selected = code, message = $"Eliminazione database non riuscita: {exception.Message}", success = false });
        }
    }
    public async Task<IActionResult> OnPostDeleteAsync(int code, CancellationToken cancellationToken)
    {
        if (!auth.IsLoggedIn())
        {
            return RedirectToPage("/Admin/Login");
        }

        var result = await repository.DeleteCompanyAsync(code, cancellationToken);
        return RedirectToPage(new { message = result.Message, success = result.Deleted });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var rows = await repository.ListCompaniesAsync(cancellationToken);
        var search = Search?.Trim() ?? "";
        if (search.Length > 0)
        {
            rows = rows
                .Where(company =>
                    company.Code.ToString("0000").Contains(search, StringComparison.OrdinalIgnoreCase)
                    || company.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || company.DatabaseName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        Companies = rows;
        Selected ??= Companies.FirstOrDefault()?.Code;
    }
}

