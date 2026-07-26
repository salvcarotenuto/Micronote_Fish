using MicronoteFood.Web.Data;
using MicronoteFood.Web.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MicronoteFood.Web.Pages.AttivitaUtenti;

public sealed class IndexModel(ActivityRepository repository) : PageModel
{
    public IReadOnlyList<ActivityListItem> Activities { get; private set; } = [];

    public IReadOnlyList<SelectListItem> YearOptions { get; private set; } = [];

    public IReadOnlyList<SelectListItem> MonthOptions { get; private set; } = [];

    public IReadOnlyList<SelectListItem> DayOptions { get; private set; } = [];

    public IReadOnlyList<SelectListItem> UserOptions { get; private set; } = [];

    public IReadOnlyList<SelectListItem> ActionOptions { get; private set; } = [];

    public int Year { get; private set; }

    public int Month { get; private set; }

    public int Day { get; private set; }

    public int UserCode { get; private set; }

    public int Action { get; private set; }

    public async Task OnGetAsync(
        int? year,
        int? month,
        int? day,
        int? userCode,
        int? action,
        CancellationToken cancellationToken)
    {
        var years = repository.GetYearOptions();
        Year = year is not null && years.Contains(year.Value)
            ? year.Value
            : years[0];
        Month = month is >= 1 and <= 12 ? month.Value : 0;
        Day = day is >= 1 and <= 31 ? day.Value : 0;
        UserCode = userCode.GetValueOrDefault();
        Action = action.GetValueOrDefault();

        var maxDay = Month > 0
            ? DateTime.DaysInMonth(Year, Month)
            : 31;
        if (Day > maxDay)
        {
            Day = 0;
        }

        YearOptions = years
            .Select(value => new SelectListItem(value.ToString(), value.ToString(), value == Year))
            .ToList();
        MonthOptions = BuildMonthOptions();
        DayOptions = Enumerable.Range(1, maxDay)
            .Select(value => new SelectListItem(value.ToString("00"), value.ToString(), value == Day))
            .ToList();
        UserOptions = (await repository.GetUserOptionsAsync(cancellationToken))
            .Select(user => new SelectListItem(
                $"{user.Code:000} - {user.DisplayName}" +
                    (string.IsNullOrWhiteSpace(user.UserName) || user.UserName == user.DisplayName
                        ? ""
                        : $" ({user.UserName})"),
                user.Code.ToString(),
                user.Code == UserCode))
            .ToList();
        ActionOptions = repository.GetActionOptions()
            .Select(option => new SelectListItem(option.Description, option.Code.ToString(), option.Code == Action))
            .ToList();

        Activities = await repository.SearchAsync(
            new ActivitySearchFilter(Year, Month, Day, UserCode, Action),
            cancellationToken);
    }

    private List<SelectListItem> BuildMonthOptions()
    {
        var names = new[]
        {
            "Gennaio",
            "Febbraio",
            "Marzo",
            "Aprile",
            "Maggio",
            "Giugno",
            "Luglio",
            "Agosto",
            "Settembre",
            "Ottobre",
            "Novembre",
            "Dicembre"
        };

        return names
            .Select((name, index) => new SelectListItem(name, (index + 1).ToString(), index + 1 == Month))
            .ToList();
    }
}
