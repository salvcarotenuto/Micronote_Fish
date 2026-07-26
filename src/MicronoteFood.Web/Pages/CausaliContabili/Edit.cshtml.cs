using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.CausaliContabili;

public class EditModel(AccountingCauseRepository repository) : PageModel
{
    [BindProperty]
    public AccountingCauseEditModel Cause { get; set; } = new();

    [BindProperty]
    public int Azione { get; set; } = FormAzione.Inserimento;

    public bool IsNew => FormAzione.IsInserimento(Azione);

    public IReadOnlyList<AccountingCauseAccountOption> AccountOptions { get; private set; } = [];

    public IReadOnlyList<AccountingCauseOption> MovementTypeOptions { get; } =
        AccountingCauseRepository.MovementTypeOptions;

    public IReadOnlyList<AccountingCauseOption> SubjectOptions { get; } =
        AccountingCauseRepository.SubjectOptions;

    public IReadOnlyList<AccountingCauseOption> SignOptions { get; } =
        AccountingCauseRepository.SignOptions;

    public IReadOnlyList<AccountingCauseOption> CashFlowOptions { get; } =
        AccountingCauseRepository.CashFlowOptions;

    public async Task<IActionResult> OnGetAsync(
        int? code,
        int? azione,
        bool duplicate,
        CancellationToken cancellationToken)
    {
        Azione = ResolveAzione(azione, code.HasValue && !duplicate);
        if (code.HasValue)
        {
            var cause = await repository.GetAsync(code.Value, cancellationToken);
            if (cause is null)
            {
                return NotFound();
            }

            Cause = cause;
            if (duplicate)
            {
                Azione = FormAzione.Inserimento;
                Cause.IsNew = true;
                Cause.Code = await repository.NextCodeAsync(cancellationToken);
                Cause.Description = "";
                Cause.Locked = false;
            }
        }
        else
        {
            Cause.IsNew = true;
            Cause.Code = await repository.NextCodeAsync(cancellationToken);
        }

        await LoadLookupsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Cause.IsNew = IsNew;
        Normalize();
        ValidateRequiredFields();
        ValidateRows();

        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(cancellationToken);
            return Page();
        }

        SetCashFromCashAccounts();

        if (IsNew)
        {
            if (await repository.ExistsAsync(Cause.Code, cancellationToken))
            {
                ModelState.AddModelError("Cause.Code", "Codice già esistente.");
                await LoadLookupsAsync(cancellationToken);
                return Page();
            }

            await repository.InsertAsync(Cause, cancellationToken);
        }
        else
        {
            var updated = await repository.UpdateAsync(Cause, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }
        }

        return RedirectToPage("./Index");
    }

    private async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        AccountOptions = await repository.GetAccountOptionsAsync(cancellationToken);
    }

    private static int ResolveAzione(int? azione, bool hasRecord)
    {
        if (azione is > 0)
        {
            return FormAzione.Normalize(azione.Value, FormAzione.ForRecord(hasRecord));
        }

        return FormAzione.ForRecord(hasRecord);
    }

    private void Normalize()
    {
        Cause.Description = ToItalianTitleCase(Cause.Description);
        Cause.MovementType = NormalizeCode(Cause.MovementType);
        Cause.Subject = NormalizeCode(Cause.Subject);
        Cause.Sign = NormalizeCode(Cause.Sign);
        Cause.CashFlow = NormalizeCode(Cause.CashFlow);
    }

    private void ValidateRows()
    {
        var debitRows = new[] { Cause.Debit1, Cause.Debit2, Cause.Debit3, Cause.Debit4, Cause.Debit5, Cause.Debit6 };
        var creditRows = new[] { Cause.Credit1, Cause.Credit2, Cause.Credit3, Cause.Credit4, Cause.Credit5, Cause.Credit6 };

        if (!HasAnyCode(debitRows))
        {
            ModelState.AddModelError("Cause.Debit1", "La sezione Dare non può essere vuota.");
        }

        if (!HasAnyCode(creditRows))
        {
            ModelState.AddModelError("Cause.Credit1", "La sezione Avere non può essere vuota.");
        }

        if (HasGaps(debitRows))
        {
            ModelState.AddModelError("Cause.Debit1", "Le righe Dare non possono avere salti vuoti.");
        }

        if (HasGaps(creditRows))
        {
            ModelState.AddModelError("Cause.Credit1", "Le righe Avere non possono avere salti vuoti.");
        }
    }

    private void ValidateRequiredFields()
    {
        if (!string.IsNullOrWhiteSpace(Cause.Description))
        {
            return;
        }

        if (!ModelState.TryGetValue("Cause.Description", out var entry) || entry.Errors.Count == 0)
        {
            ModelState.AddModelError("Cause.Description", "Campo Descrizione obbligatorio");
        }
    }

    private void SetCashFromCashAccounts()
    {
        var accounts = new[]
        {
            Cause.Debit1, Cause.Debit2, Cause.Debit3, Cause.Debit4, Cause.Debit5, Cause.Debit6,
            Cause.Credit1, Cause.Credit2, Cause.Credit3, Cause.Credit4, Cause.Credit5, Cause.Credit6
        };

        if (accounts.Any(code => code is 61 or 65))
        {
            Cause.Cash = true;
        }
    }

    private static bool HasGaps(IEnumerable<int?> codes)
    {
        var foundEmpty = false;
        foreach (var code in codes)
        {
            if (code.GetValueOrDefault() <= 0)
            {
                foundEmpty = true;
                continue;
            }

            if (foundEmpty)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnyCode(IEnumerable<int?> codes) =>
        codes.Any(code => code.GetValueOrDefault() > 0);

    private static string ToItalianTitleCase(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        var culture = CultureInfo.GetCultureInfo("it-IT");
        return culture.TextInfo.ToTitleCase(trimmed.ToLower(culture));
    }

    private static string NormalizeCode(string? value) =>
        value?.Trim().ToUpperInvariant() ?? "";

    public AccountingCauseAccountOption? GetAccount(int? code) =>
        AccountOptions.FirstOrDefault(option => option.AccountCode == code.GetValueOrDefault());
}
