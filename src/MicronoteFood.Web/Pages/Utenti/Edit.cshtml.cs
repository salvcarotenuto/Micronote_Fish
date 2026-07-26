using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Pages.Utenti;

public class EditModel(UserRepository repository) : PageModel
{
    [BindProperty]
    public UserEditModel AppUser { get; set; } = new();

    [BindProperty]
    public int Azione { get; set; } = FormAzione.Inserimento;

    public bool IsNew => FormAzione.IsInserimento(Azione);

    public int DisplayCode { get; private set; }

    public SelectList Stores { get; private set; } = EmptySelectList();
    public SelectList Types { get; private set; } = EmptySelectList();
    public SelectList Qualifications { get; private set; } = EmptySelectList();
    public SelectList Genders { get; private set; } = EmptySelectList();

    public async Task<IActionResult> OnGetAsync(
        int? code,
        int? azione,
        CancellationToken cancellationToken)
    {
        Azione = ResolveAzione(azione, code.HasValue);
        if (code.HasValue)
        {
            var user = await repository.GetAsync(code.Value, cancellationToken);
            if (user is null)
            {
                return NotFound();
            }

            AppUser = user;
        }
        else
        {
            DisplayCode = await repository.NextCodeAsync(cancellationToken);
        }

        await LoadLookupsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Azione = FormAzione.Normalize(Azione, FormAzione.ForRecord(AppUser.Code > 0));
        Normalize();

        if (!ModelState.IsValid)
        {
            if (AppUser.Code == 0)
            {
                DisplayCode = await repository.NextCodeAsync(cancellationToken);
            }

            PromoteFirstModelErrorToSummary();
            await LoadLookupsAsync(cancellationToken);
            return Page();
        }

        try
        {
            if (AppUser.Code == 0)
            {
                await repository.InsertAsync(AppUser, cancellationToken);
            }
            else
            {
                var updated = await repository.UpdateAsync(AppUser, cancellationToken);
                if (!updated)
                {
                    return NotFound();
                }
            }
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadLookupsAsync(cancellationToken);
            return Page();
        }
        catch (MySqlException exception)
        {
            ModelState.AddModelError(
                string.Empty,
                $"Salvataggio non riuscito: {exception.Message}");
            await LoadLookupsAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage("/Utenti/Index");
    }

    private static int ResolveAzione(int? azione, bool hasRecord)
    {
        if (azione is > 0)
        {
            return FormAzione.Normalize(azione.Value, FormAzione.ForRecord(hasRecord));
        }

        return FormAzione.ForRecord(hasRecord);
    }

    private async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        var lookups = await repository.GetLookupsAsync(cancellationToken);
        Stores = Select(lookups.Stores);
        Types = Select(lookups.Types);
        Qualifications = Select(lookups.Qualifications);
        Genders = Select(lookups.Genders);
    }

    private void Normalize()
    {
        AppUser.LastName = AppUser.LastName?.Trim() ?? "";
        AppUser.FirstName = AppUser.FirstName?.Trim() ?? "";
        AppUser.City = AppUser.City?.Trim();
        AppUser.Address = AppUser.Address?.Trim();
        AppUser.TaxCode = AppUser.TaxCode?.Trim().ToUpperInvariant();
        AppUser.Phone = AppUser.Phone?.Trim();
        AppUser.Email = AppUser.Email?.Trim();
        AppUser.UserName = AppUser.UserName?.Trim() ?? "";
        AppUser.Password = AppUser.Password?.Trim() ?? "";
        AppUser.Gender = AppUser.Gender?.Trim();
    }

    private void PromoteFirstModelErrorToSummary()
    {
        if (ModelState[string.Empty]?.Errors.Count > 0)
        {
            return;
        }

        var firstError = ModelState
            .SelectMany(entry => entry.Value?.Errors ?? [])
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

        if (!string.IsNullOrWhiteSpace(firstError))
        {
            ModelState.AddModelError(string.Empty, firstError);
        }
    }

    private static SelectList Select(IReadOnlyList<LookupOption> items) =>
        new(items, nameof(LookupOption.Code), nameof(LookupOption.Description));

    private static SelectList EmptySelectList() =>
        new(Array.Empty<LookupOption>());
}
