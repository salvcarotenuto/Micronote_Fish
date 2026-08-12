using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record TextLookupOption(string Code, string Description);

public sealed record ArticleListItem(
    string Code,
    string Description,
    string SalesUnitCode,
    string PurchaseUnitCode,
    int? CategoryCode,
    string CategoryDescription,
    int? GroupCode,
    string GroupDescription,
    int? SpeciesCode,
    string SpeciesDescription,
    int? OriginCode,
    string OriginDescription,
    decimal StandardCost,
    decimal StandardPrice,
    decimal VatIncludedPrice,
    decimal InitialWeight,
    decimal Tare,
    decimal VatRate,
    int? SupplierCode,
    string SupplierName);

public sealed record ArticlePrintRequest(IReadOnlyList<string> Codes);

public sealed class ArticleEditModel
{
    [Required(ErrorMessage = "Campo Codice obbligatorio")]
    [StringLength(30)]
    [Display(Name = "Codice")]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Campo Descrizione obbligatorio")]
    [StringLength(254)]
    [Display(Name = "Descrizione")]
    public string Description { get; set; } = "";

    [Display(Name = "Unità misura acquisti")]
    public string? SalesUnitCode { get; set; }

    [Display(Name = "Unità misura vendite")]
    public string? PurchaseUnitCode { get; set; }

    [Display(Name = "Categoria")]
    public int? CategoryCode { get; set; }

    [Display(Name = "Gruppo")]
    public int? GroupCode { get; set; }

    [Display(Name = "Specie")]
    public int? SpeciesCode { get; set; }

    [Display(Name = "Provenienza")]
    public int? OriginCode { get; set; }

    [Display(Name = "Aliquota iva")]
    public decimal? VatRate { get; set; }

    [Display(Name = "Tara")]
    public decimal? Tare { get; set; }

    [Display(Name = "Costo standard")]
    public decimal? StandardCost { get; set; }

    [Display(Name = "Prezzo standard")]
    public decimal? StandardPrice { get; set; }

    [Display(Name = "Giacenza iniziale colli")]
    public int? InitialPackages { get; set; }

    [Display(Name = "Giacenza iniziale peso")]
    public decimal? InitialWeight { get; set; }

    [Display(Name = "Prezzo ivato")]
    public decimal? VatIncludedPrice { get; set; }

    [Display(Name = "Fornitore abituale")]
    public int? SupplierCode { get; set; }

    [Display(Name = "Codice articolo")]
    public string? SupplierArticleCode { get; set; }

    public string SupplierName { get; set; } = "";

    public decimal? AverageCost { get; set; }

    public decimal? LastCost { get; set; }
}

public sealed record ArticleLookups(
    IReadOnlyList<TextLookupOption> UnitMeasures,
    IReadOnlyList<LookupOption> Categories,
    IReadOnlyList<LookupOption> Groups,
    IReadOnlyList<LookupOption> Species,
    IReadOnlyList<LookupOption> Origins,
    IReadOnlyList<LookupOption> Suppliers);

public sealed record ArticleDeleteResult(bool Deleted, string Message);
