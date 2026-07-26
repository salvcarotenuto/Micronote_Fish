using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record ChartAccountListItem(
    int Code,
    string Description,
    int MasterCode,
    string MasterDescription,
    string Type,
    string CompanyType,
    bool StockLoad,
    bool Locked);

public sealed class ChartAccountEditModel
{
    public bool IsNew { get; set; }

    [Range(1, short.MaxValue, ErrorMessage = "Campo Codice obbligatorio")]
    [Display(Name = "Codice")]
    public int Code { get; set; }

    [Required(ErrorMessage = "Campo Descrizione obbligatorio")]
    [StringLength(50)]
    [Display(Name = "Descrizione")]
    public string Description { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Campo Mastro obbligatorio")]
    [Display(Name = "Mastro")]
    public int MasterCode { get; set; }

    [Display(Name = "Tipo conto")]
    public string Type { get; set; } = "";

    [Display(Name = "Tipo ditta")]
    public string? CompanyType { get; set; }

    [Display(Name = "Carico magazzino")]
    public bool StockLoad { get; set; }

    [Display(Name = "Bloccato")]
    public bool Locked { get; set; }
}

public sealed record ChartAccountTypeOption(
    string Code,
    string Description);

public sealed record ChartAccountCompanyTypeOption(
    string Code,
    string Description);

public sealed record ChartAccountDeleteResult(bool Deleted, string Message);
