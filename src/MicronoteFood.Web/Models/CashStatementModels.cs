namespace MicronoteFood.Web.Models;

public sealed class CashStatementPageModel
{
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public int CashType { get; set; }
    public int StoreCode { get; set; }
    public IReadOnlyList<CashStatementStore> Stores { get; set; } = [];
    public IReadOnlyList<CashStatementRow> Rows { get; set; } = [];
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal Balance => TotalIncome - TotalExpense;
}

public sealed record CashStatementStore(int Code, string Name);

public sealed record CashStatementRow(
    int Id,
    int Year,
    int Sector,
    int Code,
    DateOnly MovementDate,
    string DocumentNumber,
    int? CauseCode,
    string CauseDescription,
    decimal Income,
    decimal Expense,
    int AccountCode,
    string AccountDescription,
    string StoreName);
