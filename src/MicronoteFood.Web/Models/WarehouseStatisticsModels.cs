namespace MicronoteFood.Web.Models;

public sealed class WarehouseStatisticsPageModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Grouping { get; set; } = "article";
    public bool IsLoaded { get; set; }
    public IReadOnlyList<WarehouseStatisticsRow> Rows { get; set; } = [];
}

public sealed record WarehouseStatisticsRow(
    string Code,
    string Description,
    decimal PurchasedQuantity,
    decimal PurchasedAmount,
    decimal AverageCost,
    decimal SoldQuantity,
    decimal SoldAmount,
    decimal RemainingQuantity,
    decimal AveragePrice,
    decimal Revenue,
    decimal Markup);

public sealed record WarehouseStatisticsCustomer(string Code, string Name, decimal Quantity, decimal Amount);
