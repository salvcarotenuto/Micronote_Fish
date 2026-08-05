using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record CashCauseListItem(
    int Code,
    string Description,
    string MovementType,
    string SubjectType,
    bool Locked);

public sealed class CashCauseEditModel
{
    public bool IsNew { get; set; }

    [Range(1, 999, ErrorMessage = "Il codice deve essere compreso tra 1 e 999.")]
    [Display(Name = "Codice")]
    public int Code { get; set; }

    [Required(ErrorMessage = "Campo Descrizione obbligatorio")]
    [StringLength(100)]
    [Display(Name = "Descrizione")]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Selezionare il tipo movimento")]
    [RegularExpression("[CR]", ErrorMessage = "Tipo movimento non valido")]
    [Display(Name = "Tipo movimento")]
    public string MovementType { get; set; } = "";

    [Required(ErrorMessage = "Selezionare il tipo ditta")]
    [RegularExpression("[CFA]", ErrorMessage = "Tipo ditta non valido")]
    [Display(Name = "Tipo ditta")]
    public string SubjectType { get; set; } = "";

    [Display(Name = "Bloccata")]
    public bool Locked { get; set; }
}

public sealed record CashCauseDeleteResult(bool Deleted, string Message);
