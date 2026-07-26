using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.Admin.Aziende;

public sealed class EditModel(
    SystemAdminAuthService auth,
    MasterRepository repository,
    MicronoteDatabaseOptions databaseOptions) : PageModel
{
    [BindProperty]
    public bool IsNew { get; set; }

    [BindProperty]
    public int Code { get; set; }

    [BindProperty]
    public string Name { get; set; } = "";

    [BindProperty]
    public string? CompanySecret { get; set; }

    [BindProperty]
    public string DatabaseName { get; set; } = "";

    [BindProperty]
    public string? CurrentDatabaseVersion { get; set; }

    [BindProperty]
    public string? RequiredDatabaseVersion { get; set; }

    [BindProperty]
    public bool Active { get; set; } = true;

    [BindProperty]
    public bool Locked { get; set; }

    public string Title => IsNew ? "Nuova azienda" : "Modifica azienda";

    public string? SaveMessage { get; private set; }

    public bool SaveSucceeded { get; private set; } = true;

    public async Task<IActionResult> OnGetAsync(int? code, CancellationToken cancellationToken)
    {
        if (!auth.IsLoggedIn())
        {
            return RedirectToPage("/Admin/Login");
        }

        if (code is null or <= 0)
        {
            Code = await repository.NextCompanyCodeAsync(cancellationToken);
            IsNew = true;
            Active = true;
            DatabaseName = databaseOptions.BuildCompanyDatabaseName(Code);
            return Page();
        }

        var company = await repository.GetCompanyEditAsync(code.Value, cancellationToken);
        if (company is null)
        {
            return RedirectToPage("Index", new { message = "Azienda non trovata." });
        }

        LoadFromCompany(company);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!auth.IsLoggedIn())
        {
            return RedirectToPage("/Admin/Login");
        }

        Name = Name?.Trim() ?? "";
        CompanySecret = CompanySecret?.Trim() ?? "";
        DatabaseName = databaseOptions.BuildCompanyDatabaseName(Code);
        CurrentDatabaseVersion = CurrentDatabaseVersion?.Trim();
        RequiredDatabaseVersion = RequiredDatabaseVersion?.Trim();

        var company = ToCompany();
        var errors = await repository.ValidateCompanyAsync(company, cancellationToken);
        foreach (var error in errors)
        {
            ModelState.AddModelError(MapField(error.Key), error.Value);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var wasNew = IsNew;
        if (IsNew)
        {
            await repository.InsertCompanyAsync(company, cancellationToken);
        }
        else if (!await repository.UpdateCompanyAsync(company, cancellationToken))
        {
            return RedirectToPage("Index", new { message = "Azienda non trovata." });
        }

        var saved = await repository.GetCompanyEditAsync(Code, cancellationToken);
        if (saved is not null)
        {
            LoadFromCompany(saved);
        }

        IsNew = false;
        SaveMessage = wasNew ? "Azienda inserita." : "Azienda aggiornata.";
        ModelState.Clear();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateDatabaseAsync(CancellationToken cancellationToken)
    {
        if (!auth.IsLoggedIn())
        {
            return RedirectToPage("/Admin/Login");
        }

        var result = await repository.CreateCompanyDatabaseAsync(Code, cancellationToken);
        await ReloadCompanyAsync(cancellationToken);
        SaveMessage = result.Message;
        SaveSucceeded = result.Success;
        ModelState.Clear();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteDatabaseAsync(CancellationToken cancellationToken)
    {
        if (!auth.IsLoggedIn())
        {
            return RedirectToPage("/Admin/Login");
        }

        var result = await repository.DeleteCompanyDatabaseAsync(Code, cancellationToken);
        await ReloadCompanyAsync(cancellationToken);
        SaveMessage = result.Message;
        SaveSucceeded = result.Success;
        ModelState.Clear();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteCompanyAsync(CancellationToken cancellationToken)
    {
        if (!auth.IsLoggedIn())
        {
            return RedirectToPage("/Admin/Login");
        }

        var result = await repository.DeleteCompanyAsync(Code, cancellationToken);
        return RedirectToPage("Index", new { message = result.Message });
    }

    private async Task ReloadCompanyAsync(CancellationToken cancellationToken)
    {
        var saved = await repository.GetCompanyEditAsync(Code, cancellationToken);
        if (saved is not null)
        {
            LoadFromCompany(saved);
            return;
        }

        DatabaseName = databaseOptions.BuildCompanyDatabaseName(Code);
    }
    private CompanyMasterEditModel ToCompany() => new()
    {
        Code = Code,
        Name = Name,
        Password = CompanySecret ?? "",
        DatabaseName = DatabaseName,
        CurrentDatabaseVersion = CurrentDatabaseVersion,
        RequiredDatabaseVersion = RequiredDatabaseVersion,
        Active = Active,
        Locked = Locked,
        IsNew = IsNew
    };

    private void LoadFromCompany(CompanyMasterEditModel company)
    {
        Code = company.Code;
        Name = company.Name;
        CompanySecret = company.Password;
        DatabaseName = databaseOptions.BuildCompanyDatabaseName(company.Code);
        CurrentDatabaseVersion = company.CurrentDatabaseVersion;
        RequiredDatabaseVersion = company.RequiredDatabaseVersion;
        Active = company.Active;
        Locked = company.Locked;
        IsNew = company.IsNew;
    }

    private static string MapField(string field) => field switch
    {
        nameof(CompanyMasterEditModel.Password) => nameof(CompanySecret),
        nameof(CompanyMasterEditModel.DatabaseName) => nameof(DatabaseName),
        nameof(CompanyMasterEditModel.Name) => nameof(Name),
        nameof(CompanyMasterEditModel.Code) => nameof(Code),
        _ => field
    };
}







