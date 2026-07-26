using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MicronoteFood.Web.Pages;

public sealed class LoginModel(
    ApplicationAuthService auth,
    UserRepository userRepository,
    ActivityLogService activityLog) : PageModel
{
    [BindProperty]
    public string CompanyName { get; set; } = "";

    [BindProperty]
    public string CompanyPassword { get; set; } = "";

    [BindProperty]
    public int? UserCode { get; set; }

    [BindProperty]
    public string UserPassword { get; set; } = "";

    public IReadOnlyList<SelectListItem> UserOptions { get; private set; } = [];

    public bool ShowUserLogin { get; private set; }

    public string SelectedCompanyName { get; private set; } = "";

    public string? ErrorMessage { get; private set; }

    public string? ErrorDetail { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (auth.IsCompanyLoggedIn())
        {
            await activityLog.LogCurrentUserAsync(
                "Utenti",
                FormAzione.Logout,
                cancellationToken: cancellationToken);
        }

        auth.Logout();
        CompanyName = "";
        CompanyPassword = "";
        UserCode = null;
        UserPassword = "";
        return Page();
    }

    public async Task<IActionResult> OnGetUserAsync(CancellationToken cancellationToken)
    {
        if (!auth.IsCompanySelected())
        {
            return RedirectToPage("/Login");
        }

        if (auth.IsCompanyLoggedIn())
        {
            await activityLog.LogCurrentUserAsync(
                "Utenti",
                FormAzione.Logout,
                cancellationToken: cancellationToken);
        }

        auth.LogoutUser();
        UserCode = null;
        UserPassword = "";
        await PrepareUserLoginAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var result = await auth.LoginCompanyAsync(
            CompanyName,
            CompanyPassword,
            cancellationToken);

        CompanyPassword = "";
        if (result.Success)
        {
            await PrepareUserLoginAsync(cancellationToken);
            return Page();
        }

        ErrorMessage = result.Message;
        ErrorDetail = result.Detail;
        return Page();
    }

    public async Task<IActionResult> OnPostUserAsync(CancellationToken cancellationToken)
    {
        var result = await auth.LoginUserAsync(
            UserCode,
            UserPassword,
            userRepository,
            cancellationToken);

        UserPassword = "";
        if (result.Success)
        {
            return RedirectToPage("/Index");
        }

        await PrepareUserLoginAsync(cancellationToken);
        ErrorMessage = result.Message;
        ErrorDetail = result.Detail;
        return Page();
    }

    private async Task PrepareUserLoginAsync(CancellationToken cancellationToken)
    {
        ShowUserLogin = true;
        SelectedCompanyName = auth.SelectedCompanyName ?? "";
        var users = await userRepository.GetLoginOptionsAsync(cancellationToken);
        UserOptions = users
            .Select(user => new SelectListItem(
                string.IsNullOrWhiteSpace(user.DisplayName)
                    ? $"{user.Code:000} - {user.UserName}"
                    : $"{user.Code:000} - {user.DisplayName} ({user.UserName})",
                user.Code.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();

        if (!UserCode.HasValue && users.Count == 1)
        {
            UserCode = users[0].Code;
        }

        if (users.Count == 0 && ErrorMessage is null)
        {
            ErrorMessage = "Nessun utente attivo disponibile.";
            ErrorDetail = "Registrare almeno un utente attivo nell'azienda selezionata.";
        }
    }
}




