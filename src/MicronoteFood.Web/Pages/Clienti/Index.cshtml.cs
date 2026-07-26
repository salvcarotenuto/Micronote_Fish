using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.Clienti;

public class IndexModel(
    CustomerRepository repository,
    CustomerListPdfService pdfService) : PageModel
{
    public IReadOnlyList<CustomerListItem> Customers { get; private set; } = [];

    public IReadOnlyList<LookupOption> Accounts { get; private set; } = [];

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        int code,
        CancellationToken cancellationToken)
    {
        var result = await repository.DeleteAsync(code, cancellationToken);
        if (!result.Deleted)
        {
            ErrorMessage = result.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPdfAsync(
        [FromBody] CustomerPrintRequest request,
        CancellationToken cancellationToken)
    {
        var allCustomers = await repository.SearchAsync(
            search: null,
            accountCode: null,
            cancellationToken: cancellationToken);
        var customersByCode = allCustomers.ToDictionary(customer => customer.Code);
        var orderedCustomers = request.Codes
            .Distinct()
            .Where(customersByCode.ContainsKey)
            .Select(code => customersByCode[code])
            .ToArray();

        var now = DateTime.Now;
        var pdf = pdfService.Create(orderedCustomers, now);
        return File(pdf, "application/pdf", $"ListaClienti_{now:yyyyMMdd_HHmm}.pdf");
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Customers = await repository.SearchAsync(
            search: null,
            accountCode: null,
            cancellationToken: cancellationToken);
        Accounts = (await repository.GetLookupsAsync(cancellationToken)).Accounts;
    }
}
