using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages;

public sealed class LogoutModel(
    ApplicationAuthService auth,
    ActivityLogService activityLog) : PageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await activityLog.LogCurrentUserAsync(
            "Utenti",
            FormAzione.Logout,
            cancellationToken: cancellationToken);
        auth.Logout();
        return RedirectToPage("/Login");
    }
}
