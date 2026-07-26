using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.Fornitori;

public class IndexModel(
    SupplierRepository repository,
    SupplierListPdfService pdfService) : PageModel
{
    public IReadOnlyList<SupplierListItem> Suppliers { get; private set; } = [];

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
        [FromBody] SupplierPrintRequest request,
        CancellationToken cancellationToken)
    {
        var allSuppliers = await repository.SearchAsync(
            search: null,
            accountCode: null,
            cancellationToken: cancellationToken);
        var suppliersByCode = allSuppliers.ToDictionary(supplier => supplier.Code);
        var orderedSuppliers = request.Codes
            .Distinct()
            .Where(suppliersByCode.ContainsKey)
            .Select(code => suppliersByCode[code])
            .ToArray();

        var pdf = pdfService.Create(orderedSuppliers, DateTime.Now);
        return File(pdf, "application/pdf", $"ListaFornitori_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Suppliers = await repository.SearchAsync(
            search: null,
            accountCode: null,
            cancellationToken: cancellationToken);
        Accounts = (await repository.GetLookupsAsync(cancellationToken)).Accounts;
    }
}
