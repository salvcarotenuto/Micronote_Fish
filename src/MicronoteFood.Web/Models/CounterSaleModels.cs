namespace MicronoteFood.Web.Models;

public sealed class CounterSalePageData
{
    public int Year { get; init; }
    public DateOnly Date { get; init; }
    public bool EnableAmountEditing { get; init; }
    public string InitialGrouping { get; init; } = "category";
    public IReadOnlyList<CounterSaleArticle> Articles { get; init; } = [];
    public IReadOnlyList<CounterSaleCustomer> Customers { get; init; } = [];
}

public sealed record CounterSaleArticle(
    string Code,
    string Description,
    string Unit,
    int CategoryCode,
    string Category,
    int GroupCode,
    string Group,
    int SpeciesCode,
    string Species,
    int OriginCode,
    string Origin,
    decimal Tare,
    decimal Stock,
    decimal Price,
    decimal VatRate);

public sealed record CounterSaleCustomer(
    int Code,
    string Name,
    int StoreCode);

public sealed class CounterSaleSaveModel
{
    public int CustomerCode { get; set; }
    public decimal Discount { get; set; }
    public List<CounterSaleSaveRow> Rows { get; set; } = [];
}

public sealed class CounterSaleSaveRow
{
    public string ArticleCode { get; set; } = "";
    public string Unit { get; set; } = "";
    public int Packages { get; set; }
    public decimal Tare { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal VatRate { get; set; }
    public decimal Amount { get; set; }
}

public sealed record CounterSaleSaveResult(int Id, int Code);
