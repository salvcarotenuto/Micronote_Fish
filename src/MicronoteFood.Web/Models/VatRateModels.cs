using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record VatRateListItem(
    string Code,
    string Description,
    decimal Rate,
    decimal Deduction,
    string ElectronicInvoiceNature);

public sealed record VatNatureOption(
    string Code,
    string Description);

public sealed class VatRateEditModel
{
    public bool IsNew { get; set; }

    [Required(ErrorMessage = "Campo Codice obbligatorio")]
    [StringLength(10)]
    [Display(Name = "Codice")]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Campo Descrizione obbligatorio")]
    [StringLength(120)]
    [Display(Name = "Descrizione")]
    public string Description { get; set; } = "";

    [Display(Name = "Aliquota")]
    public decimal Rate { get; set; }

    [Display(Name = "Detrazione")]
    public decimal Deduction { get; set; }

    [StringLength(10)]
    [Display(Name = "F.E. cod. esenz.")]
    public string? ElectronicInvoiceNature { get; set; }
}

public sealed record VatRateDeleteResult(bool Deleted, string Message);
