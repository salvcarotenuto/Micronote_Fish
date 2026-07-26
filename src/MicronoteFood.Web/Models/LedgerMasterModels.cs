using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record LedgerMasterListItem(
    int Code,
    string Description,
    string Type,
    bool Locked);

public sealed class LedgerMasterEditModel
{
    public bool IsNew { get; set; }

    [Range(1, short.MaxValue, ErrorMessage = "Campo Codice obbligatorio")]
    [Display(Name = "Codice")]
    public int Code { get; set; }

    [Required(ErrorMessage = "Campo Descrizione obbligatorio")]
    [StringLength(120)]
    [Display(Name = "Descrizione")]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Campo Tipo obbligatorio")]
    [Display(Name = "Tipo")]
    public string Type { get; set; } = "";

    [Display(Name = "Bloccato")]
    public bool Locked { get; set; }
}

public sealed record LedgerMasterTypeOption(
    string Code,
    string Description);

public sealed record LedgerMasterDeleteResult(bool Deleted, string Message);
