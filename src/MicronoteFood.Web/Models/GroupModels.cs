using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record GroupListItem(
    int Code,
    string Description);

public sealed class GroupEditModel
{
    public int Code { get; set; }

    [Required(ErrorMessage = "Campo Descrizione obbligatorio")]
    [StringLength(120)]
    [Display(Name = "Descrizione")]
    public string Description { get; set; } = "";
}

public sealed record GroupDeleteResult(bool Deleted, string Message);
