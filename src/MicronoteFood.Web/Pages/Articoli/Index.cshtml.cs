using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using MicronoteFood.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MicronoteFood.Web.Pages.Articoli;

public class IndexModel(
    ArticleRepository repository,
    ArticleListPdfService pdfService) : PageModel
{
    public IReadOnlyList<ArticleListItem> Articles { get; private set; } = [];

    public ArticleLookups Lookups { get; private set; } =
        new([], [], [], [], []);

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Articles = await repository.ListAsync(cancellationToken);
        Lookups = await repository.GetLookupsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var result = await repository.DeleteAsync(code, cancellationToken);
        if (!result.Deleted)
        {
            ErrorMessage = result.Message;
            Articles = await repository.ListAsync(cancellationToken);
            Lookups = await repository.GetLookupsAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPdfAsync(
        [FromBody] ArticlePrintRequest request,
        CancellationToken cancellationToken)
    {
        var allArticles = await repository.ListAsync(cancellationToken);
        var articlesByCode = allArticles.ToDictionary(article => article.Code, StringComparer.OrdinalIgnoreCase);
        var orderedArticles = request.Codes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(articlesByCode.ContainsKey)
            .Select(code => articlesByCode[code])
            .ToArray();

        var now = DateTime.Now;
        var pdf = pdfService.Create(orderedArticles, now);
        return File(pdf, "application/pdf", $"ListaArticoli_{now:yyyyMMdd_HHmm}.pdf");
    }

    public IEnumerable<SelectListItem> CategoryItems =>
        Lookups.Categories.Select(item => new SelectListItem(item.Description, item.Code.ToString()));

    public IEnumerable<SelectListItem> GroupItems =>
        Lookups.Groups.Select(item => new SelectListItem(item.Description, item.Code.ToString()));

    public IEnumerable<SelectListItem> SubgroupItems =>
        Lookups.Subgroups.Select(item => new SelectListItem(item.Description, item.Code.ToString()));

    public IEnumerable<SelectListItem> SupplierItems =>
        Lookups.Suppliers.Select(item => new SelectListItem(item.Description, item.Code.ToString()));
}
