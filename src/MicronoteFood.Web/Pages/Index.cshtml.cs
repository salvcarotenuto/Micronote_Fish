using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages;

public class IndexModel(
    ApplicationState applicationState,
    ApplicationAuthService auth,
    CurrentCompanyContext companyContext) : PageModel
{
    public IReadOnlyList<QuickLinkDefinition> QuickLinks => MainMenuCatalog.QuickLinks;

    public IReadOnlyList<MenuSectionDefinition> Sections => MainMenuCatalog.Sections;

    public int Esercizio => applicationState.Esercizio;

    public string CompanyName => companyContext.Name ?? "";

    public string UserName => HttpContext.Session.GetString(ApplicationAuthService.UserNameKey) ?? "";

    public IActionResult OnGet()
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        if (!auth.IsCompanyLoggedIn())
        {
            return RedirectToPage("/Login");
        }

        return Page();
    }
}


