using System.Globalization;
using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;

namespace MicronoteFood.Web.Services;

public sealed class ApplicationAuthService(
    IHttpContextAccessor httpContextAccessor,
    MasterRepository masterRepository,
    ActivityLogService activityLog)
{
    public const string CompanyCodeKey = "micronote_company_code";
    public const string CompanyNameKey = "micronote_company_name";
    public const string CompanyDatabaseKey = "micronote_company_database";
    public const string UserCodeKey = "micronote_user_code";
    public const string UserNameKey = "micronote_user_name";
    public const string UserDisplayNameKey = "micronote_user_display_name";
    private const string LastSeenKey = "micronote_company_last_seen";
    private const int IdleTimeoutSeconds = 1800;

    public bool IsCompanyLoggedIn()
    {
        var session = httpContextAccessor.HttpContext?.Session;
        if (session is null)
        {
            return false;
        }

        var codeText = session.GetString(CompanyCodeKey);
        if (!int.TryParse(codeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
            || code <= 0)
        {
            return false;
        }

        var userCodeText = session.GetString(UserCodeKey);
        if (!int.TryParse(userCodeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userCode)
            || userCode <= 0)
        {
            return false;
        }

        var lastSeen = session.GetString(LastSeenKey);
        if (long.TryParse(lastSeen, out var ticks))
        {
            var elapsed = DateTimeOffset.UtcNow - new DateTimeOffset(ticks, TimeSpan.Zero);
            if (elapsed.TotalSeconds > IdleTimeoutSeconds)
            {
                Logout();
                return false;
            }
        }

        Touch(session);
        return true;
    }

    public bool IsCompanySelected()
    {
        var session = httpContextAccessor.HttpContext?.Session;
        if (session is null)
        {
            return false;
        }

        var codeText = session.GetString(CompanyCodeKey);
        return int.TryParse(codeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
            && code > 0;
    }

    public string? SelectedCompanyName =>
        httpContextAccessor.HttpContext?.Session.GetString(CompanyNameKey);

    public async Task<ApplicationCompanyLoginResult> LoginCompanyAsync(
        string? companyName,
        string? companyPassword,
        CancellationToken cancellationToken = default)
    {
        var cleanCompanyName = companyName?.Trim() ?? "";
        var cleanPassword = companyPassword?.Trim() ?? "";
        if (cleanCompanyName.Length == 0)
        {
            return new(false, "Inserire il nome azienda.", "", null);
        }

        if (cleanPassword.Length == 0)
        {
            return new(false, "Inserire la password azienda.", "", null);
        }

        var result = await masterRepository.CheckCompanyLoginAsync(
            cleanCompanyName,
            cleanPassword,
            cancellationToken);

        if (result.Status != CompanyLoginStatus.Success || result.Company is null)
        {
            return new(false, result.Message, CompanyLoginDetail(result.Status), result.Company);
        }

        var session = httpContextAccessor.HttpContext?.Session;
        if (session is null)
        {
            return new(false, "Sessione non disponibile.", "Impossibile completare l'accesso all'applicazione.", null);
        }

        session.Clear();
        session.SetString(CompanyCodeKey, result.Company.Code.ToString(CultureInfo.InvariantCulture));
        session.SetString(CompanyNameKey, result.Company.Name);
        session.SetString(CompanyDatabaseKey, result.Company.DatabaseName);
        Touch(session);

        return new(true, "", "", result.Company);
    }

    public async Task<ApplicationUserLoginResult> LoginUserAsync(
        int? userCode,
        string? userPassword,
        UserRepository userRepository,
        CancellationToken cancellationToken = default)
    {
        var cleanPassword = userPassword?.Trim() ?? "";
        if (!IsCompanySelected())
        {
            return new(false, "Selezionare prima l'azienda.", "", null);
        }

        if (!userCode.HasValue || userCode.Value <= 0)
        {
            return new(false, "Selezionare l'utente.", "", null);
        }

        if (cleanPassword.Length == 0)
        {
            return new(false, "Inserire la password utente.", "", null);
        }

        var result = await userRepository.CheckLoginAsync(
            userCode.Value,
            cleanPassword,
            cancellationToken);

        if (result.Status != UserLoginStatus.Success || result.User is null)
        {
            return new(false, result.Message, UserLoginDetail(result.Status), result.User);
        }

        var session = httpContextAccessor.HttpContext?.Session;
        if (session is null)
        {
            return new(false, "Sessione non disponibile.", "Impossibile completare l'accesso all'applicazione.", null);
        }

        session.SetString(UserCodeKey, result.User.Code.ToString(CultureInfo.InvariantCulture));
        session.SetString(UserNameKey, result.User.UserName);
        session.SetString(UserDisplayNameKey, result.User.DisplayName);
        Touch(session);
        await activityLog.LogForUserAsync(
            result.User.Code,
            "Utenti",
            FormAzione.Login,
            result.User.Code.ToString(CultureInfo.InvariantCulture),
            cancellationToken);

        return new(true, "", "", result.User);
    }

    public void LogoutUser()
    {
        var session = httpContextAccessor.HttpContext?.Session;
        session?.Remove(UserCodeKey);
        session?.Remove(UserNameKey);
        session?.Remove(UserDisplayNameKey);
        session?.Remove(LastSeenKey);
    }

    public void Logout()
    {
        var session = httpContextAccessor.HttpContext?.Session;
        session?.Remove(CompanyCodeKey);
        session?.Remove(CompanyNameKey);
        session?.Remove(CompanyDatabaseKey);
        LogoutUser();
    }

    private static string CompanyLoginDetail(CompanyLoginStatus status) => status switch
    {
        CompanyLoginStatus.CompanyNotFound => "Controllare il nome azienda indicato.",
        CompanyLoginStatus.InvalidCompanyPassword => "Controllare la password azienda.",
        CompanyLoginStatus.CompanyInactive => "L'azienda selezionata non risulta attiva.",
        CompanyLoginStatus.CompanyLocked => "L'accesso all'azienda selezionata risulta bloccato.",
        CompanyLoginStatus.CompanyDatabaseMissing => "Il database aziendale non e' stato trovato.",
        CompanyLoginStatus.CompanyDatabaseNotReady => "Il database aziendale non e' pronto per l'uso.",
        _ => ""
    };

    private static string UserLoginDetail(UserLoginStatus status) => status switch
    {
        UserLoginStatus.UserNotFound => "L'utente indicato non e' presente nell'azienda selezionata.",
        UserLoginStatus.InvalidPassword => "Controllare la password utente.",
        UserLoginStatus.UserInactive => "L'utente non risulta attivo.",
        UserLoginStatus.UserLocked => "L'utente risulta bloccato.",
        _ => ""
    };

    private static void Touch(ISession session) =>
        session.SetString(
            LastSeenKey,
            DateTimeOffset.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
}

public sealed record ApplicationCompanyLoginResult(
    bool Success,
    string Message,
    string Detail,
    CompanyMasterRecord? Company);

public sealed record ApplicationUserLoginResult(
    bool Success,
    string Message,
    string Detail,
    UserLoginOption? User);



