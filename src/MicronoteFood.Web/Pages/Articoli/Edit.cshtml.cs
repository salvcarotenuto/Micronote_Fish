using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Globalization;

namespace MicronoteFood.Web.Pages.Articoli;

public class EditModel(ArticleRepository repository) : PageModel
{
    [BindProperty]
    public ArticleEditModel Article { get; set; } = new();

    [BindProperty]
    public string ReturnUrl { get; set; } = "/Articoli/Index";

    [BindProperty]
    public int Azione { get; set; } = FormAzione.Inserimento;

    public bool IsNew => FormAzione.IsInserimento(Azione);

    public bool IsModal => FormAzione.IsModale(Azione);

    public bool IsCodeReadonly => FormAzione.IsCodiceBloccato(Azione);

    public bool IsReadonly => FormAzione.IsReadonly(Azione);

    public ArticleLookups Lookups { get; private set; } =
        new([], [], [], [], []);

    public async Task<IActionResult> OnGetAsync(
        string? code,
        int? azione,
        bool modal,
        string? prefillCode,
        string? description,
        string? unitMeasure,
        string? standardCost,
        string? vatRate,
        int? supplierCode,
        string? supplierArticleCode,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        Azione = ResolveAzione(azione, modal, code, prefillCode);
        ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
            ? "/Articoli/Index"
            : returnUrl;
        Lookups = await repository.GetLookupsAsync(cancellationToken);

        if (IsNew)
        {
            Article = new ArticleEditModel
            {
                Code = prefillCode?.Trim() ?? "",
                Description = description?.Trim() ?? "",
                UnitMeasureCode = unitMeasure?.Trim(),
                StandardCost = ParseDecimal(standardCost),
                VatRate = ParseDecimal(vatRate),
                SupplierCode = supplierCode is > 0 ? supplierCode : null,
                SupplierArticleCode = supplierArticleCode?.Trim()
            };
            Article.SupplierName = Article.SupplierCode is null
                ? ""
                : Lookups.Suppliers.FirstOrDefault(item => item.Code == Article.SupplierCode.Value)?.Description ?? "";
            return Page();
        }

        var article = await repository.GetAsync(code!, cancellationToken);
        if (article is null)
        {
            return RedirectToPage("./Index");
        }

        Article = article;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        ReturnUrl = string.IsNullOrWhiteSpace(ReturnUrl)
            ? "/Articoli/Index"
            : ReturnUrl;
        if (!ModelState.IsValid)
        {
            Lookups = await repository.GetLookupsAsync(cancellationToken);
            return Page();
        }

        if (IsNew)
        {
            if (await repository.ExistsAsync(Article.Code, cancellationToken))
            {
                ModelState.AddModelError(
                    nameof(Article.Code),
                    "Codice articolo già presente in archivio.");
                Lookups = await repository.GetLookupsAsync(cancellationToken);
                return Page();
            }

            await repository.InsertAsync(Article, cancellationToken);
        }
        else if (!await repository.UpdateAsync(Article, cancellationToken))
        {
            ModelState.AddModelError("", "Aggiornamento non riuscito.");
            Lookups = await repository.GetLookupsAsync(cancellationToken);
            return Page();
        }

        if (IsModal)
        {
            return Content(
                """
                <!doctype html>
                <html>
                <body>
                <script>
                window.parent.postMessage({ type: "micronote:article-saved", code: "__ARTICLE_CODE__" }, window.location.origin);
                </script>
                </body>
                </html>
                """.Replace("__ARTICLE_CODE__", JavaScriptStringEncode(Article.Code.Trim())),
                "text/html");
        }

        return Redirect(ReturnUrl);
    }

    private static int ResolveAzione(int? azione, bool modal, string? code, string? prefillCode)
    {
        if (azione is > 0)
        {
            return azione.Value;
        }

        var baseAction = string.IsNullOrWhiteSpace(code)
            ? FormAzione.Inserimento
            : FormAzione.Modifica;
        var contexts = new List<int>();
        if (modal)
        {
            contexts.Add(FormAzione.Modale);
        }

        if (!string.IsNullOrWhiteSpace(prefillCode))
        {
            contexts.Add(FormAzione.OrigineFe);
            contexts.Add(FormAzione.CodiceBloccato);
        }

        return FormAzione.WithContesto(baseAction, contexts.ToArray());
    }

    public IEnumerable<SelectListItem> UnitMeasureItems =>
        Lookups.UnitMeasures.Select(item => new SelectListItem(item.Description, item.Code));

    public IEnumerable<SelectListItem> CategoryItems =>
        Lookups.Categories.Select(item => new SelectListItem(item.Description, item.Code.ToString()));

    public IEnumerable<SelectListItem> GroupItems =>
        Lookups.Groups.Select(item => new SelectListItem(item.Description, item.Code.ToString()));

    public IEnumerable<SelectListItem> SubgroupItems =>
        Lookups.Subgroups.Select(item => new SelectListItem(item.Description, item.Code.ToString()));

    public IEnumerable<SelectListItem> SupplierItems =>
        Lookups.Suppliers.Select(item => new SelectListItem(item.Description, item.Code.ToString()));

    private static decimal? ParseDecimal(string? value)
    {
        var text = (value ?? "").Trim().Replace("%", "").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("it-IT"), out var italianValue))
        {
            return italianValue;
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantValue)
            ? invariantValue
            : null;
    }

    private static string JavaScriptStringEncode(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
}
