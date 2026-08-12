namespace MicronoteFood.Web.Models;

public sealed class DailyTradingPageModel
{
    public DateOnly MovementDate { get; set; }
    public string Grouping { get; set; } = "";
    public IReadOnlyList<DailyTradingRow> Rows { get; set; } = [];
    public DailyTradingTotals Totals { get; set; } = new();
}

public sealed record DailyTradingRow(
    string Code,
    string Description,
    decimal PurchasedQuantity,
    decimal PurchasedValue,
    decimal AverageCost,
    decimal SoldQuantity,
    decimal SoldValue,
    decimal AveragePrice,
    decimal QuantityBalance,
    decimal RemainingQuantity);

public sealed class DailyTradingTotals
{
    public decimal Purchases { get; set; }
    public decimal PurchasesPaid { get; set; }
    public decimal Sales { get; set; }
    public decimal SalesPaid { get; set; }
    public decimal TradingBalance => Sales - Purchases;
    public decimal CashBalance => SalesPaid - PurchasesPaid;
}
