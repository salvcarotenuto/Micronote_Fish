namespace MicronoteFood.Web.Models;

public sealed class TrialBalancePageModel
{
    public int Year { get; set; }
    public int? StoreCode { get; set; }
    public bool IsLoaded { get; set; }
    public IReadOnlyList<int> Years { get; set; } = [];
    public IReadOnlyList<TrialBalanceStoreOption> Stores { get; set; } = [];
    public IReadOnlyList<TrialBalanceEquityRow> EquityRows { get; set; } = [];
    public IReadOnlyList<TrialBalanceIncomeRow> IncomeRows { get; set; } = [];
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal TotalCosts { get; set; }
    public decimal TotalRevenues { get; set; }
    public decimal ProfitOrLoss => Math.Round(TotalRevenues - TotalCosts, 2);
    public int RecordCount => EquityRows.Count + IncomeRows.Count;
}

public sealed record TrialBalanceStoreOption(int Code, string Name);

public sealed record TrialBalanceEquityRow(
    int AccountCode,
    string Description,
    decimal Assets,
    decimal Liabilities);

public sealed record TrialBalanceIncomeRow(
    int AccountCode,
    string Description,
    decimal Costs,
    decimal Revenues,
    decimal Share,
    string Kind);

public sealed record TrialBalancePrintRequest(int Year);
