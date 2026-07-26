using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record AccountingCauseListItem(
    int Code,
    string Description,
    string MovementType,
    string Subject,
    string Sign,
    string CashFlow,
    int DebitCode,
    string DebitDescription,
    int CreditCode,
    string CreditDescription,
    bool Cash,
    bool Invoice,
    bool DueDate,
    bool Title,
    bool Salary,
    bool Print,
    bool Locked);

public sealed class AccountingCauseEditModel
{
    public bool IsNew { get; set; }

    [Range(1, short.MaxValue, ErrorMessage = "Campo Codice obbligatorio")]
    [Display(Name = "Codice")]
    public int Code { get; set; }

    [Required(ErrorMessage = "Campo Descrizione obbligatorio")]
    [StringLength(100)]
    [Display(Name = "Descrizione")]
    public string Description { get; set; } = "";

    [Display(Name = "Tipo movimento")]
    public string? MovementType { get; set; }

    [Display(Name = "Soggetto")]
    public string? Subject { get; set; }

    [Display(Name = "Segno")]
    public string? Sign { get; set; }

    [Display(Name = "Entrata/Uscita")]
    public string? CashFlow { get; set; }

    [Display(Name = "Cassa")]
    public bool Cash { get; set; }

    [Display(Name = "Richiama fattura")]
    public bool Invoice { get; set; }

    [Display(Name = "Richiama scadenza")]
    public bool DueDate { get; set; }

    [Display(Name = "Richiama titolo")]
    public bool Title { get; set; }

    [Display(Name = "Stipendio")]
    public bool Salary { get; set; }

    [Display(Name = "Stampa")]
    public bool Print { get; set; }

    [Display(Name = "Bloccato")]
    public bool Locked { get; set; }

    public int? Debit1 { get; set; }
    public int? Debit2 { get; set; }
    public int? Debit3 { get; set; }
    public int? Debit4 { get; set; }
    public int? Debit5 { get; set; }
    public int? Debit6 { get; set; }

    public int? Credit1 { get; set; }
    public int? Credit2 { get; set; }
    public int? Credit3 { get; set; }
    public int? Credit4 { get; set; }
    public int? Credit5 { get; set; }
    public int? Credit6 { get; set; }
}

public sealed record AccountingCauseOption(string Code, string Description);

public sealed record AccountingCauseAccountOption(
    int AccountCode,
    string AccountDescription,
    int MasterCode,
    string MasterDescription,
    string Type);

public sealed record AccountingCauseDeleteResult(bool Deleted, string Message);
