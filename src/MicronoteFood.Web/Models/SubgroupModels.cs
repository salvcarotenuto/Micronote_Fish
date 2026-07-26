using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record SubgroupListItem(
    int Code,
    string Description);

public sealed class SubgroupEditModel
{
    public int Code { get; set; }

    [Required(ErrorMessage = "Campo Descrizione obbligatorio")]
    [StringLength(120)]
    [Display(Name = "Descrizione")]
    public string Description { get; set; } = "";
}

public sealed record SubgroupDeleteResult(bool Deleted, string Message);
