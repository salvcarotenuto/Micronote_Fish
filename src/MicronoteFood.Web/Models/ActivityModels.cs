namespace MicronoteFood.Web.Models;

public sealed record ActivityActionOption(int Code, string Description);

public sealed record ActivityUserOption(int Code, string UserName, string DisplayName);

public sealed record ActivityListItem(
    int Id,
    DateOnly? Date,
    TimeOnly? Time,
    int CompanyCode,
    int UserCode,
    string UserName,
    string UserDisplayName,
    string TableName,
    int Action,
    string ActionDescription,
    int Year,
    string Number,
    string Code);

public sealed record ActivitySearchFilter(
    int Year,
    int Month,
    int Day,
    int UserCode,
    int Action);
