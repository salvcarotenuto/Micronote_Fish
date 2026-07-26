using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record UnitMeasureListItem(
    string Code,
    string Description);

public sealed class UnitMeasureEditModel
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
}

public sealed record UnitMeasureDeleteResult(bool Deleted, string Message);
