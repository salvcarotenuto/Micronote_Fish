using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MySqlConnector;

namespace MicronoteFood.Web.Pages.PianoConti;

public class EditModel(ChartAccountRepository repository) : PageModel
{
    [BindProperty]
    public ChartAccountEditModel Account { get; set; } = new();

    [BindProperty]
    public int Azione { get; set; } = FormAzione.Inserimento;

    public bool IsNew => FormAzione.IsInserimento(Azione);

    public bool IsModal => FormAzione.IsModale(Azione);

    public SelectList Masters { get; private set; } = EmptySelectList();

    public IReadOnlyDictionary<int, string> MasterTypes { get; private set; } =
        new Dictionary<int, string>();

    public IReadOnlyList<ChartAccountCompanyTypeOption> CompanyTypeOptions { get; } =
        ChartAccountRepository.CompanyTypeOptions;

    public async Task<IActionResult> OnGetAsync(
        int? code,
        int? azione,
        bool modal,
        string? companyType,
        CancellationToken cancellationToken)
    {
        Azione = ResolveAzione(azione, modal, code);
        if (code.HasValue)
        {
            var account = await repository.GetAsync(code.Value, cancellationToken);
            if (account is null)
            {
                return NotFound();
            }

            Account = account;
        }
        else
        {
            Account.IsNew = true;
            Account.Code = await repository.NextCodeAsync(cancellationToken);
            Account.CompanyType = NormalizeCode(companyType);
            Account.Type = "";
        }

        await LoadLookupsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var modalContext = FormAzione.IsModale(Azione);
        Azione = FormAzione.Normalize(
            Azione,
            FormAzione.ForRecord(
                !Account.IsNew,
                modalContext ? FormAzione.Modale : FormAzione.Nessuna));
        Account.IsNew = IsNew;
        Normalize();
        Account.Type = ChartAccountRepository.TypeDescription(
            await repository.MasterTypeAsync(Account.MasterCode, cancellationToken));
        ModelState.Remove("Account.Type");

        if (IsNew)
        {
            Account.Code = await repository.NextCodeAsync(cancellationToken);
            ModelState.Remove("Account.Code");
        }

        if (!ModelState.IsValid)
        {
            PromoteFirstModelErrorToSummary();
            await LoadLookupsAsync(cancellationToken);
            return Page();
        }

        try
        {
            if (IsNew)
            {
                await repository.InsertAsync(Account, cancellationToken);
            }
            else
            {
                var updated = await repository.UpdateAsync(Account, cancellationToken);
                if (!updated)
                {
                    return NotFound();
                }
            }
        }
        catch (MySqlException ex)
        {
            ModelState.AddModelError(string.Empty, $"Registrazione non riuscita: {ex.Message}");
            await LoadLookupsAsync(cancellationToken);
            return Page();
        }

        if (IsModal)
        {
            return ModalSavedResult();
        }

        return RedirectToPage("./Index");
    }

    private async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        Masters = Select(await repository.GetMasterOptionsAsync(cancellationToken));
        MasterTypes = await repository.GetMasterTypesAsync(cancellationToken);
    }

    private void Normalize()
    {
        Account.Description = Account.Description?.Trim() ?? "";
        Account.CompanyType = NormalizeCode(Account.CompanyType);
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

    private static string? NormalizeCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static SelectList Select(IReadOnlyList<LookupOption> items) =>
        new(items, nameof(LookupOption.Code), nameof(LookupOption.Description));

    private static SelectList EmptySelectList() =>
        new(Array.Empty<LookupOption>());

    private static ContentResult ModalSavedResult() =>
        new()
        {
            ContentType = "text/html; charset=utf-8",
            Content = """
                <!doctype html>
                <html>
                <head><meta charset="utf-8"><title>Conto salvato</title></head>
                <body>
                <script>
                window.parent.postMessage({ type: "micronote:chart-account-saved" }, window.location.origin);
                </script>
                </body>
                </html>
                """
        };

    private static int ResolveAzione(int? azione, bool modal, int? code)
    {
        if (azione is > 0)
        {
            return FormAzione.Normalize(azione.Value, FormAzione.ForRecord(code.HasValue));
        }

        return FormAzione.ForRecord(
            code.HasValue,
            modal ? FormAzione.Modale : FormAzione.Nessuna);
    }
}
