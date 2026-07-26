using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record BankListItem(int Code, string Name, string Branch, string Abi, string Cab, string Account);

public sealed class BankEditModel
{
    public int Code { get; set; }

    [Required(ErrorMessage = "Campo Nome obbligatorio")]
    [StringLength(100)]
    public string Name { get; set; } = "";

    [StringLength(50)] public string? Branch { get; set; }
    [StringLength(100)] public string? Website { get; set; }
    [StringLength(100), EmailAddress(ErrorMessage = "Indirizzo e-mail non valido.")] public string? Email { get; set; }
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(30)] public string? Mobile { get; set; }
    [StringLength(5)] public string? Abi { get; set; }
    [StringLength(5)] public string? Cab { get; set; }
    [StringLength(12)] public string? Swift { get; set; }
    [StringLength(10)] public string? Sia { get; set; }
    [StringLength(25)] public string? Account { get; set; }
    [StringLength(50)] public string? Iban { get; set; }
    [StringLength(255)] public string? Notes { get; set; }
}

public sealed record BankDeleteResult(bool Deleted, string Message);
