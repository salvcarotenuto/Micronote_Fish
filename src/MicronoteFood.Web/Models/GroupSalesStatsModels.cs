namespace MicronoteFood.Web.Models;

public sealed class GroupSalesStatsPageModel
{
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public string Grouping { get; set; } = "category";
    public bool IsLoaded { get; set; }
    public IReadOnlyList<GroupSalesStatsStore> Stores { get; set; } = [];
    public IReadOnlyList<GroupSalesStatsRow> Rows { get; set; } = [];
}

public sealed record GroupSalesStatsStore(int Code, string Name);
public sealed record GroupSalesStatsCell(int StoreCode, decimal Amount, decimal Share);
public sealed record GroupSalesStatsRow(int Code, string Description, decimal Amount, IReadOnlyList<GroupSalesStatsCell> Stores);
public sealed record GroupSalesStatsArticle(string Code, string Description, decimal Amount);
public sealed record GroupSalesStatsCustomer(int Code, string Name, decimal Amount);
public sealed record GroupSalesStatsDetails(IReadOnlyList<GroupSalesStatsArticle> Articles, IReadOnlyList<GroupSalesStatsCustomer> Customers);
