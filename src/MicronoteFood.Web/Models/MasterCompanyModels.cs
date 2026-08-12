using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed record CompanyMasterRecord(
    int Code,
    string Name,
    string Password,
    bool Active,
    bool Locked,
    string DatabaseName,
    DateTime? CurrentDatabaseVersion,
    DateTime? RequiredDatabaseVersion,
    bool DatabaseExists = false);

public sealed record AppParameterRecord(string Key, string Value);

public enum CompanyLoginStatus
{
    Success,
    CompanyNotFound,
    InvalidCompanyPassword,
    CompanyInactive,
    CompanyLocked,
    CompanyDatabaseMissing,
    CompanyDatabaseNotReady
}

public sealed record CompanyLoginResult(
    CompanyLoginStatus Status,
    CompanyMasterRecord? Company,
    string Message);

public sealed class CompanyMasterEditModel
{
    [Display(Name = "Codice")]
    [Range(1, 9999, ErrorMessage = "Indicare un codice azienda valido.")]
    public int Code { get; set; }

    [Display(Name = "Nome accesso")]
    [Required(ErrorMessage = "Campo Nome accesso obbligatorio")]
    [StringLength(120, ErrorMessage = "Il campo Nome accesso non puo superare 120 caratteri.")]
    public string Name { get; set; } = "";

    [Display(Name = "Password")]
    [Required(ErrorMessage = "Campo Password obbligatorio")]
    [StringLength(255, ErrorMessage = "Il campo Password non puo superare 255 caratteri.")]
    public string Password { get; set; } = "";

    [Display(Name = "Nome database")]
    [Required(ErrorMessage = "Campo Nome database obbligatorio")]
    [StringLength(120, ErrorMessage = "Il campo Nome database non puo superare 120 caratteri.")]
    public string DatabaseName { get; set; } = "";

    [Display(Name = "Versione DB attuale")]
    public DateTime? CurrentDatabaseVersion { get; set; }

    [Display(Name = "Versione DB richiesta")]
    public DateTime? RequiredDatabaseVersion { get; set; }

    [Display(Name = "Attiva")]
    public bool Active { get; set; } = true;

    [Display(Name = "Bloccata")]
    public bool Locked { get; set; }

    public bool IsNew { get; set; } = true;
}

public sealed record CompanyMasterDeleteResult(bool Deleted, string Message);


public sealed record CompanyDatabaseServiceResult(bool Success, string Message);

