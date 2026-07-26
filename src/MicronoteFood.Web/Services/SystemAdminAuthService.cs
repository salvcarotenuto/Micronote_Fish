using System.Globalization;
namespace MicronoteFood.Web.Services;

public sealed class SystemAdminAuthService(
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor)
{
    private const string SessionKey = "micronote_system_admin";
    private const string LastSeenKey = "micronote_system_admin_last_seen";
    private const int IdleTimeoutSeconds = 1800;

    public bool IsLoggedIn()
    {
        var session = httpContextAccessor.HttpContext?.Session;
        if (session is null)
        {
            return false;
        }

        var userName = session.GetString(SessionKey);
        if (!string.Equals(userName, "admin", StringComparison.OrdinalIgnoreCase))
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

    public SystemAdminLoginResult Login(string? userName, string? password)
    {
        var cleanUserName = userName?.Trim() ?? "";
        var cleanPassword = password?.Trim() ?? "";
        if (cleanUserName.Length == 0 || cleanPassword.Length == 0)
        {
            return new SystemAdminLoginResult(
                false,
                "Inserire username e password.",
                "");
        }

        var configuredPassword = AdminPassword();
        if (!string.Equals(cleanUserName, "admin", StringComparison.OrdinalIgnoreCase)
            || configuredPassword.Length == 0
            || !string.Equals(configuredPassword, cleanPassword, StringComparison.Ordinal))
        {
            return new SystemAdminLoginResult(
                false,
                "Credenziali amministrative non valide.",
                "Controllare username e password.");
        }

        var session = httpContextAccessor.HttpContext?.Session;
        if (session is null)
        {
            return new SystemAdminLoginResult(
                false,
                "Sessione non disponibile.",
                "Impossibile completare l'accesso amministrativo.");
        }

        session.Clear();
        session.SetString(SessionKey, "admin");
        Touch(session);

        return new SystemAdminLoginResult(true, "", "");
    }

    public void Logout()
    {
        var session = httpContextAccessor.HttpContext?.Session;
        session?.Remove(SessionKey);
        session?.Remove(LastSeenKey);
    }

    private string AdminPassword() =>
        configuration["Micronote:Admin:Password"]
        ?? Environment.GetEnvironmentVariable("MICRONOTE_APP_ADMIN_PASSWORD")
        ?? "";

    private static void Touch(ISession session) =>
        session.SetString(
            LastSeenKey,
            DateTimeOffset.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
}

public sealed record SystemAdminLoginResult(
    bool Success,
    string Message,
    string Detail);


