using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record TextLookupOption(string Code, string Description);

public sealed record ArticleListItem(
    string Code,
    string Description,
    string UnitMeasureCode,
    int? CategoryCode,
    string CategoryDescription,
    int? GroupCode,
    string GroupDescription,
    int? SubgroupCode,
    string SubgroupDescription,
    decimal StandardCost,
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

    [Display(Name = "Unità di misura")]
    public string? UnitMeasureCode { get; set; }

    [Display(Name = "Categoria")]
    public int? CategoryCode { get; set; }

    [Display(Name = "Gruppo")]
    public int? GroupCode { get; set; }

    [Display(Name = "Sottogruppo")]
    public int? SubgroupCode { get; set; }

    [Display(Name = "Aliquota iva")]
    public decimal? VatRate { get; set; }

    [Display(Name = "Peso in confezione")]
    public decimal? NetWeight { get; set; }

    [Display(Name = "Pezzi in confezione")]
    public int? Pieces { get; set; }

    [Display(Name = "Costo standard")]
    public decimal? StandardCost { get; set; }

    [Display(Name = "Prezzo standard")]
    public decimal? StandardPrice { get; set; }

    [Display(Name = "Giacenza iniziale")]
    public decimal? InitialStock { get; set; }

    [Display(Name = "Consumo giornaliero")]
    public decimal? DailyConsumption { get; set; }

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
    IReadOnlyList<LookupOption> Subgroups,
    IReadOnlyList<LookupOption> Suppliers);

public sealed record ArticleDeleteResult(bool Deleted, string Message);
