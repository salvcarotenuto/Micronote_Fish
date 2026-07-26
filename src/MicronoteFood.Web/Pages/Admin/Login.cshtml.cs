using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.Admin;

public sealed class LoginModel(SystemAdminAuthService auth) : PageModel
{
    [BindProperty]
    public string UserName { get; set; } = "admin";

    [BindProperty]
    public string Password { get; set; } = "";

    public string? ErrorMessage { get; private set; }

    public string? ErrorDetail { get; private set; }

    public IActionResult OnGet()
    {
        if (auth.IsLoggedIn())
        {
            return RedirectToPage("/Admin/Index");
        }

        return Page();
    }

    public IActionResult OnPost()
    {
        var result = auth.Login(UserName, Password);
        if (result.Success)
        {
            return RedirectToPage("/Admin/Index");
        }

        ErrorMessage = result.Message;
        ErrorDetail = result.Detail;
        Password = "";
        return Page();
    }
}
