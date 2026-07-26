using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record PaymentCodeListItem(
    int Code,
    string Description,
    string Abbreviation,
    string PaymentType,
    int DueDates,
    string StartFrom);

public sealed class PaymentCodeEditModel
{
    public bool IsNew { get; set; }

    [Range(1, 99999, ErrorMessage = "Campo Codice obbligatorio")]
    [Display(Name = "Codice")]
    public int Code { get; set; }

    [Required(ErrorMessage = "Campo Descrizione obbligatorio")]
    [StringLength(60)]
    [Display(Name = "Descrizione")]
    public string Description { get; set; } = "";

    [StringLength(10)]
    [Display(Name = "Sigla")]
    public string? Abbreviation { get; set; }

    [Required(ErrorMessage = "Campo Tipo pagamento obbligatorio")]
    [Display(Name = "Tipo pagamento")]
    public string PaymentType { get; set; } = "";

    [Display(Name = "Tipo titolo")]
    public int? TitleType { get; set; }

    [Range(0, 99)]
    [Display(Name = "N.ro scadenze")]
    public int DueDates { get; set; }

    [Range(0, 999)]
    [Display(Name = "Primo interv.")]
    public int FirstInterval { get; set; }

    [Range(0, 999)]
    [Display(Name = "Intervallo")]
    public int Interval { get; set; }

    [Range(0, 999)]
    [Display(Name = "Slittamento")]
    public int TimeOffset { get; set; }

    [Display(Name = "Condizioni")]
    public string? Conditions { get; set; }

    [Display(Name = "Modalità")]
    public string? Mode { get; set; }

    [Display(Name = "Decorrenza")]
    public int StartFrom { get; set; }

    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "Campo Spese non valido")]
    [Display(Name = "Spese")]
    public decimal Expenses { get; set; }

    [Display(Name = "Salta agosto")]
    public bool SkipAugust { get; set; }

    [Display(Name = "Salta dicembre")]
    public bool SkipDecember { get; set; }
}

public sealed record PaymentCodeOption(string Code, string Description);

public sealed record PaymentCodeNumberOption(int Code, string Description);

public sealed record PaymentCodeDeleteResult(bool Deleted, string Message);
