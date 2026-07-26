using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record ComuneListItem(
    string Name,
    string Province,
    string PostalCode,
    string Code);

public sealed class ComuneEditModel
{
    public bool IsNew { get; set; }

    public string OriginalName { get; set; } = "";

    [Required(ErrorMessage = "Campo Nome obbligatorio")]
    [StringLength(80)]
    [Display(Name = "Nome")]
    public string Name { get; set; } = "";

    [StringLength(4)]
    [Display(Name = "Prov.")]
    public string? Province { get; set; }

    [StringLength(10)]
    [Display(Name = "CAP")]
    public string? PostalCode { get; set; }

    [StringLength(12)]
    [Display(Name = "Codice")]
    public string? Code { get; set; }
}

public sealed record ComuneDeleteResult(bool Deleted, string Message);
