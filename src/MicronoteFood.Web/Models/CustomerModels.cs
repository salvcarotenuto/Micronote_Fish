using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record CustomerListItem(
    int Code,
    string Name,
    string City,
    string Province,
    string VatNumber,
    string Phone,
    string Email,
    int? AccountCode,
    string AccountDescription,
    string StoreName,
    bool IsActive);

public sealed record CustomerPrintRequest(IReadOnlyList<int> Codes);

public sealed class CustomerEditModel
{
    public int Code { get; set; }

    [Required(ErrorMessage = "Campo Nome obbligatorio")]
    [StringLength(250)]
    [Display(Name = "Nome")]
    public string Name { get; set; } = "";

    [StringLength(16)]
    [Display(Name = "Codice fiscale")]
    public string? TaxCode { get; set; }

    [StringLength(11)]
    [Display(Name = "Partita IVA")]
    public string? VatNumber { get; set; }

    [StringLength(50)]
    [Display(Name = "Città")]
    public string? City { get; set; }

    [StringLength(5)]
    [Display(Name = "C.A.P.")]
    public string? PostalCode { get; set; }

    [StringLength(2)]
    [Display(Name = "Provincia")]
    public string? Province { get; set; }

    [StringLength(50)]
    [Display(Name = "Indirizzo")]
    public string? Address { get; set; }

    [StringLength(30)]
    [Display(Name = "Telefono")]
    public string? Phone { get; set; }

    [StringLength(30)]
    [Display(Name = "Cellulare")]
    public string? Mobile { get; set; }

    [StringLength(50)]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    [StringLength(50)]
    [Display(Name = "PEC")]
    public string? CertifiedEmail { get; set; }

    [StringLength(100)]
    [Display(Name = "Contatto")]
    public string? Contact { get; set; }

    [StringLength(30)]
    [Display(Name = "Cellulare contatto")]
    public string? ContactMobile { get; set; }

    [Display(Name = "Punto vendita")]
    public int? StoreCode { get; set; }

    [Display(Name = "Attivo")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Saldo iniziale")]
    public decimal? OpeningBalance { get; set; }

    [Display(Name = "Categoria")]
    public int? CategoryCode { get; set; }

    [Range(0, 255, ErrorMessage = "Il listino deve essere compreso tra 0 e 255.")]
    [Display(Name = "Listino")]
    public int PriceList { get; set; }

    [Display(Name = "Fido")]
    public decimal? CreditLimit { get; set; }

    [StringLength(10)]
    [Display(Name = "Codice SDI")]
    public string? SdiCode { get; set; }

    [Display(Name = "Nazione")]
    public int? CountryCode { get; set; }

    [Display(Name = "Natura giuridica")]
    public int? LegalNatureCode { get; set; }

    [Required(ErrorMessage = "Campo Contropartita obbligatorio")]
    [Display(Name = "Contropartita")]
    public int? AccountCode { get; set; }

    [Display(Name = "Agente")]
    public int? AgentCode { get; set; }

    [Display(Name = "Pagamento")]
    public int? PaymentCode { get; set; }

    [Display(Name = "Banca")]
    public int? BankCode { get; set; }
}

public sealed record CustomerLookups(
    IReadOnlyList<LookupOption> Stores,
    IReadOnlyList<LookupOption> Categories,
    IReadOnlyList<LookupOption> Countries,
    IReadOnlyList<LookupOption> LegalNatures,
    IReadOnlyList<LookupOption> Accounts,
    IReadOnlyList<LookupOption> Agents,
    IReadOnlyList<LookupOption> Payments,
    IReadOnlyList<LookupOption> Banks);

public sealed record CustomerDeleteResult(bool Deleted, string Message);
