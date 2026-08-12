namespace MicronoteFood.Web.Models;

public sealed class DailySalesPageModel
{
    public DateOnly SaleDate { get; set; }
    public int StoreCode { get; set; }
    public string SelectedKey { get; set; } = "";
    public IReadOnlyList<DailySaleItem> Sales { get; set; } = [];
    public IReadOnlyList<DailySoldArticle> Articles { get; set; } = [];
    public IReadOnlyList<DailySaleDetail> Details { get; set; } = [];
    public DailySaleTotals Totals { get; set; } = new();
    public IReadOnlyList<DailySaleStore> Stores { get; set; } = [];
}

public sealed record DailySaleItem(string Key, int Id, int Year, int Code, int CustomerCode, string CustomerName, decimal Goods, decimal Vat, decimal Total, decimal Paid, decimal Cash);
public sealed record DailySoldArticle(string ArticleCode, string Description, decimal Quantity, decimal Amount);
public sealed record DailySaleDetail(int RowNumber, string ArticleCode, string Description, string UnitMeasure, int Packages, decimal Quantity, decimal Price, decimal VatRate, decimal VatIncludedPrice, decimal Amount);
public sealed record DailySaleStore(int Code, string Name);

public sealed class DailySaleTotals
{
    public decimal Goods { get; set; }
    public decimal Vat { get; set; }
    public decimal Sales { get; set; }
    public decimal Paid { get; set; }
    public decimal CashIn { get; set; }
}
