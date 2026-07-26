using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Pages.CodiciPagamento;

public class EditModel(PaymentCodeRepository repository) : PageModel
{
    [BindProperty]
    public PaymentCodeEditModel Payment { get; set; } = new();

    [BindProperty]
    public int Azione { get; set; } = FormAzione.Inserimento;

    public bool IsNew => FormAzione.IsInserimento(Azione);

    public bool IsModal => FormAzione.IsModale(Azione);

    public IReadOnlyList<PaymentCodeOption> PaymentTypeOptions { get; private set; } = [];

    public IReadOnlyList<PaymentCodeNumberOption> TitleTypeOptions { get; private set; } = [];

    public IReadOnlyList<PaymentCodeOption> ConditionOptions { get; private set; } = [];

    public IReadOnlyList<PaymentCodeOption> ModeOptions { get; private set; } = [];

    public IReadOnlyList<PaymentCodeNumberOption> StartFromOptions { get; } =
        PaymentCodeRepository.StartFromOptions;

    public async Task<IActionResult> OnGetAsync(
        int? code,
        int? azione,
        bool modal,
        CancellationToken cancellationToken)
    {
        Azione = ResolveAzione(azione, modal, code);
        if (code.HasValue)
        {
            var payment = await repository.GetAsync(code.Value, cancellationToken);
            if (payment is null)
            {
                return NotFound();
            }

            Payment = payment;
        }
        else
        {
            Payment.IsNew = true;
            Payment.Code = await repository.NextCodeAsync(cancellationToken);
            Payment.DueDates = 1;
            Payment.StartFrom = 1;
        }

        await LoadLookupsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Payment.IsNew = IsNew;
        if (IsNew)
        {
            Payment.Code = await repository.NextCodeAsync(cancellationToken);
            ModelState.Remove("Payment.Code");
        }

        Normalize();

        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(cancellationToken);
            return Page();
        }

        if (IsNew)
        {
            if (await repository.ExistsAsync(Payment.Code, cancellationToken))
            {
                ModelState.AddModelError("Payment.Code", "Codice già esistente.");
                await LoadLookupsAsync(cancellationToken);
                return Page();
            }

            await repository.InsertAsync(Payment, cancellationToken);
        }
        else
        {
            var updated = await repository.UpdateAsync(Payment, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }
        }

        if (IsModal)
        {
            return ModalSavedResult();
        }

        return RedirectToPage("./Index");
    }

    public static string CurrencyText(decimal value) =>
        value.ToString("#,##0.00", CultureInfo.GetCultureInfo("it-IT"));

    private async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        PaymentTypeOptions = await repository.ListPaymentTypeOptionsAsync(cancellationToken);
        TitleTypeOptions = await repository.ListTitleTypeOptionsAsync(cancellationToken);
        ConditionOptions = await repository.ListConditionOptionsAsync(cancellationToken);
        ModeOptions = await repository.ListModeOptionsAsync(cancellationToken);
    }

    private void Normalize()
    {
        Payment.Description = ToItalianTitleCase(Payment.Description);
        Payment.Abbreviation = Payment.Abbreviation?.Trim().ToUpperInvariant() ?? "";
        Payment.PaymentType = Payment.PaymentType?.Trim().ToUpperInvariant() ?? "";
        Payment.Conditions = NormalizeOptionalCode(Payment.Conditions);
        Payment.Mode = NormalizeOptionalCode(Payment.Mode);
    }

    private static string? NormalizeOptionalCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

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

    private static ContentResult ModalSavedResult() =>
        new()
        {
            ContentType = "text/html; charset=utf-8",
            Content = """
                <!doctype html>
                <html>
                <head><meta charset="utf-8"><title>Pagamento salvato</title></head>
                <body>
                <script>
                window.parent.postMessage({ type: "micronote:payment-code-saved" }, window.location.origin);
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
