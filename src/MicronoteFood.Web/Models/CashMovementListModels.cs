namespace MicronoteFood.Web.Models;

public sealed record CashMovementListRow(
    int Id,
    int Year,
    int Sector,
    int Code,
    DateOnly MovementDate,
    int CauseCode,
    string CauseDescription,
    string Description,
    string SubjectType,
    int SubjectCode,
    string SubjectName,
    int StoreCode,
    string StoreName,
    decimal Income,
    decimal Expense,
    int PaymentMethod,
    string PaymentDescription);

public sealed record CashMovementFilterOption(int Code, string Description);

public sealed class CashMovementListPageModel
{
    public int Year { get; set; }
    public string Period { get; set; } = "";
    public string MovementType { get; set; } = "";
    public int? PaymentMethod { get; set; }
    public int? CauseCode { get; set; }
    public int? StoreCode { get; set; }
    public int? SelectedId { get; set; }
    public IReadOnlyList<int> Years { get; set; } = [];
    public IReadOnlyList<CashMovementFilterOption> Causes { get; set; } = [];
    public IReadOnlyList<CashMovementFilterOption> Stores { get; set; } = [];
    public IReadOnlyList<CashMovementListRow> Rows { get; set; } = [];
    public decimal TotalIncome => Rows.Sum(row => row.Income);
    public decimal TotalExpense => Rows.Sum(row => row.Expense);
    public decimal Balance => TotalIncome - TotalExpense;
}
