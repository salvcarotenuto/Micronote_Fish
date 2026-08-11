namespace MicronoteFood.Web.Models;

public sealed class DailyPurchasesPageModel
{
    public DateOnly PurchaseDate { get; set; }
    public string SelectedKey { get; set; } = "";
    public IReadOnlyList<DailyPurchaseItem> Purchases { get; set; } = [];
    public IReadOnlyList<DailyPurchasedArticle> Articles { get; set; } = [];
    public IReadOnlyList<DailyPurchaseDetail> Details { get; set; } = [];
    public DailyPurchaseTotals Totals { get; set; } = new();
}

public sealed record DailyPurchaseItem(
    string Key,
    int Id,
    int Year,
    int Code,
    int SupplierCode,
    string SupplierName,
    decimal Goods,
    decimal Vat,
    decimal Total,
    decimal Paid,
    decimal Cash);

public sealed record DailyPurchasedArticle(
    string ArticleCode,
    string Description,
    decimal Quantity,
    decimal Amount);

public sealed record DailyPurchaseDetail(
    int RowNumber,
    string ArticleCode,
    string Description,
    string UnitMeasure,
    int Packages,
    decimal Quantity,
    decimal Price,
    decimal VatRate,
    decimal VatIncludedPrice,
    decimal Amount);

public sealed class DailyPurchaseTotals
{
    public decimal Goods { get; set; }
    public decimal Vat { get; set; }
    public decimal Purchases { get; set; }
    public decimal Paid { get; set; }
    public decimal CashOut { get; set; }
}

public sealed class SupplierBalanceModel
{
    public int SupplierCode { get; set; }
    public string SupplierName { get; set; } = "";
    public decimal OpeningBalance { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalPayments { get; set; }
    public decimal FinalBalance => OpeningBalance + TotalPurchases - TotalPayments;
    public IReadOnlyList<SupplierBalanceRow> Rows { get; set; } = [];
}

public sealed record SupplierBalanceRow(
    DateOnly MovementDate,
    string DocumentNumber,
    string CauseDescription,
    decimal Purchases,
    decimal Payments);
