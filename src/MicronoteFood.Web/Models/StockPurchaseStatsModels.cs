namespace MicronoteFood.Web.Models;

public sealed class StockPurchaseStatsPageModel
{
    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    public int? StoreCode { get; set; }

    public string Search { get; set; } = "";

    public decimal TotalAmount { get; set; }

    public bool IsLoaded { get; set; }

    public IReadOnlyList<StockPurchaseStatsArticleItem> Articles { get; set; } = [];

    public IReadOnlyList<StockPurchaseStatsStoreOption> Stores { get; set; } = [];
}

public sealed record StockPurchaseStatsArticleItem(
    string ArticleCode,
    string Description,
    string UnitMeasure,
    decimal Quantity,
    decimal Amount,
    decimal StandardCost,
    decimal AverageCost,
    decimal ThirdLastPrice,
    decimal SecondLastPrice,
    decimal LastPrice,
    DateOnly? LastPurchaseDate,
    decimal Share)
{
    public string Key => ArticleCode;
}

public sealed record StockPurchaseStatsSupplierItem(
    int SupplierCode,
    string SupplierName,
    decimal Quantity,
    decimal Amount,
    decimal AverageCost,
    decimal LastPrice,
    DateOnly? LastPurchaseDate);

public sealed record StockPurchaseStatsStoreOption(int Code, string Name);

public sealed class GroupPurchaseStatsPageModel
{
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public string Grouping { get; set; } = "group";
    public bool IsLoaded { get; set; }
    public decimal TotalRevenue { get; set; }
    public IReadOnlyList<GroupPurchaseStatsStore> Stores { get; set; } = [];
    public IReadOnlyList<GroupPurchaseStatsRow> Rows { get; set; } = [];
}

public sealed record GroupPurchaseStatsStore(int Code, string Name, decimal Revenue);

public sealed record GroupPurchaseStatsCell(
    int StoreCode,
    decimal Amount,
    decimal CostShare,
    decimal RevenueShare);

public sealed record GroupPurchaseStatsRow(
    int Code,
    string Description,
    decimal Amount,
    IReadOnlyList<GroupPurchaseStatsCell> Stores);

public sealed record GroupPurchaseStatsArticle(string Code, string Description, decimal Amount);

public sealed record GroupPurchaseStatsSupplier(int Code, string Name, decimal Amount);

public sealed record GroupPurchaseStatsDetails(
    IReadOnlyList<GroupPurchaseStatsArticle> Articles,
    IReadOnlyList<GroupPurchaseStatsSupplier> Suppliers);
