using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record CategoryListItem(
    int Code,
    string Description);

public sealed class CategoryEditModel
{
    public int Code { get; set; }

    [Required(ErrorMessage = "Campo Descrizione obbligatorio")]
    [StringLength(120)]
    [Display(Name = "Descrizione")]
    public string Description { get; set; } = "";
}

public sealed record CategoryDeleteResult(bool Deleted, string Message);
