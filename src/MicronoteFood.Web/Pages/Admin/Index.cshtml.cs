using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.Admin;

public sealed class IndexModel(
    SystemAdminAuthService auth,
    MasterRepository masterRepository,
    MicronoteDatabaseOptions databaseOptions) : PageModel
{
    public IReadOnlyList<CompanyMasterRecord> Companies { get; private set; } = [];

    public string MasterDatabaseName => databaseOptions.MasterDatabase;

    public string? LoadError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!auth.IsLoggedIn())
        {
            return RedirectToPage("/Admin/Login");
        }

        try
        {
            Companies = await masterRepository.ListCompaniesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            LoadError = exception.Message;
        }

        return Page();
    }

    public IActionResult OnPostLogout()
    {
        auth.Logout();
        return RedirectToPage("/Index");
    }
}


