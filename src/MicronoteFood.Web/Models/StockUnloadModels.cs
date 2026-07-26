namespace MicronoteFood.Web.Models;

public sealed class StockUnloadListModel
{
    public int Year { get; set; }
    public int? Month { get; set; }
    public int? CauseCode { get; set; }
    public int? StoreCode { get; set; }
    public string SelectedKey { get; set; } = "";
    public IReadOnlyList<int> Years { get; set; } = [];
    public IReadOnlyList<LookupOption> Stores { get; set; } = [];
    public IReadOnlyList<StockUnloadListItem> Rows { get; set; } = [];
}

public sealed record StockUnloadListItem(
    int Id, int Year, int Code, DateOnly Date, int CauseCode, string Cause,
    string ArticleCode, string Description, decimal Quantity,
    int StoreCode, string Store, int SupplierCode, string Supplier)
{
    public string Key => Id.ToString();
}

public sealed class StockUnloadEditModel
{
    public int Id { get; set; }

    public bool IsNew { get; set; }

    public int Year { get; set; }
    public int Code { get; set; }
    public int CauseCode { get; set; } = 14;
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string ArticleCode { get; set; } = "";
    public string ArticleDescription { get; set; } = "";
    public string UnitMeasure { get; set; } = "";
    public decimal Stock { get; set; }
    public decimal Quantity { get; set; }
    public int StoreCode { get; set; }
    public int SupplierCode { get; set; }
    public string SupplierName { get; set; } = "";
}

public sealed record StockUnloadArticle(string Code, string Description, string UnitMeasure);
public sealed record StockUnloadSupplier(int Code, string Name);

public sealed class StockUnloadMaskModel
{
    public IReadOnlyList<LookupOption> Stores { get; set; } = [];
    public IReadOnlyList<StockUnloadArticle> Articles { get; set; } = [];
    public IReadOnlyList<StockUnloadSupplier> Suppliers { get; set; } = [];
}
