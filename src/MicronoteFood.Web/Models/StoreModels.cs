using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record StoreListItem(
    int Code,
    string Name,
    string Description,
    string SectorDescription,
    string City,
    string Province,
    string Contact,
    string Phone,
    string Mobile,
    string Email,
    bool IsActive);

public sealed class StoreEditModel
{
    public int Code { get; set; }

    [Required(ErrorMessage = "Campo Nome obbligatorio")]
    [StringLength(80)]
    [Display(Name = "Nome")]
    public string Name { get; set; } = "";

    [StringLength(160)]
    [Display(Name = "Descrizione")]
    public string? Description { get; set; }

    [StringLength(60)]
    [Display(Name = "Città")]
    public string? City { get; set; }

    [StringLength(10)]
    [Display(Name = "CAP")]
    public string? PostalCode { get; set; }

    [StringLength(120)]
    [Display(Name = "Indirizzo")]
    public string? Address { get; set; }

    [StringLength(5)]
    [Display(Name = "Prov.")]
    public string? Province { get; set; }

    [Display(Name = "Settore")]
    public int? SectorCode { get; set; }

    [Display(Name = "Punto attivo")]
    public bool IsActive { get; set; } = true;

    [StringLength(80)]
    [Display(Name = "Contatto")]
    public string? Contact { get; set; }

    [StringLength(30)]
    [Display(Name = "Telefono")]
    public string? Phone { get; set; }

    [StringLength(30)]
    [Display(Name = "Cellulare")]
    public string? Mobile { get; set; }

    [StringLength(80)]
    [EmailAddress(ErrorMessage = "Indirizzo e-mail non valido.")]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }
}

public sealed record StoreLookups(IReadOnlyList<LookupOption> Sectors);

public sealed record StoreDeleteResult(bool Deleted, string Message);
