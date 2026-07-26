using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record NationListItem(
    int Code,
    string Name,
    string Abbreviation1,
    string Abbreviation2,
    string Iso,
    string TaxCode,
    string Zone,
    string Regime);

public sealed class NationEditModel
{
    public bool IsNew { get; set; }

    [Display(Name = "Codice")]
    public int Code { get; set; }

    [Required(ErrorMessage = "Campo Nome obbligatorio")]
    [StringLength(80)]
    [Display(Name = "Nome")]
    public string Name { get; set; } = "";

    [StringLength(10)]
    [Display(Name = "Sigla 1")]
    public string? Abbreviation1 { get; set; }

    [StringLength(10)]
    [Display(Name = "Sigla 2")]
    public string? Abbreviation2 { get; set; }

    [StringLength(10)]
    [Display(Name = "ISO")]
    public string? Iso { get; set; }

    [StringLength(10)]
    [Display(Name = "Cod. fiscale")]
    public string? TaxCode { get; set; }

    [StringLength(10)]
    [Display(Name = "Zona")]
    public string? Zone { get; set; }

    [StringLength(10)]
    [Display(Name = "Regime")]
    public string? Regime { get; set; }
}

public sealed record NationDeleteResult(bool Deleted, string Message);
