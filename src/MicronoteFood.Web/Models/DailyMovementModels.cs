namespace MicronoteFood.Web.Models;

public sealed class DailyMovementPageModel
{
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public string SelectionType { get; set; } = "articolo";
    public string ArticleCode { get; set; } = "";
    public string ArticleDescription { get; set; } = "";
    public int? ClassificationCode { get; set; }
    public IReadOnlyList<DailyMovementArticleOption> Articles { get; set; } = [];
    public IReadOnlyList<LookupOption> Categories { get; set; } = [];
    public IReadOnlyList<LookupOption> Groups { get; set; } = [];
    public IReadOnlyList<LookupOption> Species { get; set; } = [];
    public IReadOnlyList<LookupOption> Origins { get; set; } = [];
    public IReadOnlyList<DailyMovementRow> Rows { get; set; } = [];
    public DailyMovementTotals Totals { get; set; } = new();
}

public sealed record DailyMovementArticleOption(string Code, string Description);

public sealed record DailyMovementRow(
    int Code,
    string MovementType,
    string Cause,
    string ArticleCode,
    string ArticleDescription,
    int CompanyCode,
    string CompanyName,
    int Packages,
    decimal Quantity,
    decimal Price,
    decimal Amount);

public sealed class DailyMovementTotals
{
    public int PurchasedPackages { get; set; }
    public decimal PurchasedQuantity { get; set; }
    public decimal PurchasedAmount { get; set; }
    public decimal PurchaseAverage => PurchasedQuantity != 0
        ? PurchasedAmount / PurchasedQuantity
        : PurchasedPackages != 0 ? PurchasedAmount / PurchasedPackages : 0;
    public int SoldPackages { get; set; }
    public decimal SoldQuantity { get; set; }
    public decimal SoldAmount { get; set; }
    public decimal SaleAverage => SoldQuantity != 0
        ? SoldAmount / SoldQuantity
        : SoldPackages != 0 ? SoldAmount / SoldPackages : 0;
    public int PackageBalance => SoldPackages - PurchasedPackages;
    public decimal QuantityBalance => SoldQuantity - PurchasedQuantity;
    public decimal AmountBalance => SoldAmount - PurchasedAmount;
    public decimal AverageMarkup => SaleAverage - PurchaseAverage;
    public decimal PercentageMarkup => PurchaseAverage == 0 ? 0 : AverageMarkup * 100 / PurchaseAverage;
}
