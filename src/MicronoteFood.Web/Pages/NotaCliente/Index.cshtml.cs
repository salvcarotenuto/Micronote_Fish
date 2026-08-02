using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicronoteFood.Web.Pages.NotaCliente;

public sealed class IndexModel(CustomerNoteRepository repository) : PageModel
{
    private const string PrintedInSessionKey = "NotaCliente.StampaEseguita";

    public CustomerNotePageModel Report { get; private set; } = new();
    public bool HasPrintedInSession => HttpContext.Session.GetString(PrintedInSessionKey) == "1";
    public DateOnly? LastProcessingDate { get; private set; }

    public async Task OnGetAsync(
        DateOnly? dateFrom,
        DateOnly? dateTo,
        int? storeCode,
        int? customerCode,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(System.DateTime.Today);
        if (dateFrom is null && dateTo is null && storeCode is null && customerCode is null)
            HttpContext.Session.Remove(PrintedInSessionKey);
        LastProcessingDate = await repository.GetLastProcessingDateAsync(cancellationToken);
        var from = dateFrom ?? LastProcessingDate?.AddDays(1) ?? today.AddDays(-6);
        var to = dateTo ?? today;
        if (from > to)
            from = to;
        Report = await repository.GetAsync(from, to, storeCode, customerCode, cancellationToken);
    }

    public IActionResult OnPostPrinted()
    {
        HttpContext.Session.SetString(PrintedInSessionKey, "1");
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostSaveProcessingDateAsync(
        DateOnly processingDate,
        CancellationToken cancellationToken)
    {
        if (HasPrintedInSession)
            await repository.SaveLastProcessingDateAsync(processingDate, cancellationToken);
        HttpContext.Session.Remove(PrintedInSessionKey);
        return RedirectToPage("/Index");
    }

}
