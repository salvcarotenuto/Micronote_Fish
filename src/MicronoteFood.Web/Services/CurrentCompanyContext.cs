using MicronoteFood.Web.Data;

namespace MicronoteFood.Web.Services;

public sealed class CurrentCompanyContext(IHttpContextAccessor httpContextAccessor)
{
    public int Code
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.Session.GetString(ApplicationAuthService.CompanyCodeKey);
            return int.TryParse(value, out var code) && code > 0
                ? code
                : MicronoteDatabaseOptions.DefaultCompanyCode;
        }
    }

    public string CodeText => MicronoteDatabaseOptions.CompanyKey(Code);

    public string? Name => httpContextAccessor.HttpContext?.Session.GetString(ApplicationAuthService.CompanyNameKey);

    public string? DatabaseName => httpContextAccessor.HttpContext?.Session.GetString(ApplicationAuthService.CompanyDatabaseKey);
}
