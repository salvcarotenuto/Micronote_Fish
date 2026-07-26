namespace MicronoteFood.Web.Models;

public sealed class StockLoadListPageModel
{
    public int Year { get; set; }

    public int? Month { get; set; }

    public int? SupplierCode { get; set; }

    public string SupplierName { get; set; } = "";

    public int? StoreCode { get; set; }

    public string ArticleCode { get; set; } = "";

    public string ArticleDescription { get; set; } = "";

    public string SelectedKey { get; set; } = "";

    public IReadOnlyList<StockLoadListItem> Documents { get; set; } = [];

    public IReadOnlyList<StockLoadDetailItem> Details { get; set; } = [];

    public StockLoadTotals Totals { get; set; } = StockLoadTotals.Empty;

    public IReadOnlyList<int> Years { get; set; } = [];

    public IReadOnlyList<PurchaseInvoiceMonthOption> Months { get; set; } = [];

    public IReadOnlyList<StockLoadSupplierOption> Suppliers { get; set; } = [];

    public IReadOnlyList<PurchaseInvoiceStoreOption> Stores { get; set; } = [];
}

public sealed record StockLoadListItem(
    string Key,
    int Id,
    bool HasId,
    int Year,
    int Code,
    string DocumentNumber,
    DateOnly DocumentDate,
    int SupplierCode,
    string SupplierName,
    decimal Goods,
    decimal Vat,
    decimal Total,
    string ElectronicInvoiceName);

public sealed record StockLoadDetailItem(
    int RowNumber,
    string ArticleCode,
    string Description,
    string UnitMeasure,
    decimal Quantity,
    decimal Price,
    decimal Discount,
    decimal Amount,
    decimal VatRate);

public sealed class StockLoadEditDocument
{
    public int Id { get; set; }

    public int Year { get; set; }

    public int Code { get; set; }

    public int CauseCode { get; set; } = 10;

    public string DocumentNumber { get; set; } = "";

    public DateOnly? DocumentDate { get; set; }

    public int SupplierCode { get; set; }

    public string SupplierName { get; set; } = "";

    public int StoreCode { get; set; }

    public string ElectronicInvoiceName { get; set; } = "";

    public IReadOnlyList<StockLoadDetailItem> Details { get; set; } = [];
}

public sealed class StockLoadSaveCommand
{
    public int Id { get; set; }

    public int Year { get; set; }

    public int Code { get; set; }

    public int CauseCode { get; set; } = 10;

    public string DocumentNumber { get; set; } = "";

    public DateOnly? DocumentDate { get; set; }

    public int SupplierCode { get; set; }

    public int StoreCode { get; set; }

    public string ElectronicInvoiceName { get; set; } = "";

    public string ElectronicInvoicePath { get; set; } = "";

    public bool AllowOverwrite { get; set; }

    public IReadOnlyList<StockLoadSaveRow> Rows { get; set; } = [];
}

public sealed record StockLoadSaveRow(
    int RowNumber,
    string ArticleCode,
    string Description,
    string UnitMeasure,
    decimal Quantity,
    decimal Price,
    decimal Discount,
    decimal Amount,
    decimal VatRate);

public sealed record StockLoadSaveResult(
    bool Success,
    string Message,
    int Id,
    int Year,
    int Code,
    bool Overwritten,
    decimal Goods,
    decimal Vat,
    decimal Total,
    bool RequiresOverwrite = false);

public sealed record StockLoadSupplierOption(
    int Code,
    string Name);

public sealed record StockLoadTotals(
    decimal Goods,
    decimal Vat,
    decimal Total)
{
    public static StockLoadTotals Empty { get; } = new(0, 0, 0);
}
