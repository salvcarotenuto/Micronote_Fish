namespace MicronoteFood.Web.Models;

public sealed class StockMovementListPageModel
{
    public int Year { get; set; }

    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    public string ArticleCode { get; set; } = "";

    public string ArticleDescription { get; set; } = "";

    public int? CategoryCode { get; set; }

    public int? GroupCode { get; set; }

    public int? SubgroupCode { get; set; }

    public bool IncludeLoads { get; set; } = true;

    public bool IncludeUnloads { get; set; } = true;

    public int? CustomerCode { get; set; }

    public string CustomerName { get; set; } = "";

    public int? SupplierCode { get; set; }

    public string SupplierName { get; set; } = "";

    public string? SelectedKey { get; set; }

    public IReadOnlyList<StockMovementListItem> Movements { get; set; } = [];

    public StockMovementTotals Totals { get; set; } = new();

    public IReadOnlyList<LookupOption> Categories { get; set; } = [];

    public IReadOnlyList<LookupOption> Groups { get; set; } = [];

    public IReadOnlyList<LookupOption> Subgroups { get; set; } = [];

    public IReadOnlyList<StockMovementSubjectOption> Customers { get; set; } = [];

    public IReadOnlyList<StockMovementSubjectOption> Suppliers { get; set; } = [];
}

public sealed record StockMovementListItem(
    int Year,
    int Sector,
    int Code,
    int RowNumber,
    int CauseCode,
    string CauseDescription,
    DateOnly MovementDate,
    string DocumentNumber,
    string ArticleCode,
    string ArticleDescription,
    string MovementType,
    string UnitMeasure,
    decimal Quantity,
    decimal Price,
    decimal Amount,
    decimal VatRate,
    string SubjectType,
    int SubjectCode,
    string SubjectName,
    int StockLoadId,
    int SaleId)
{
    public string Key => $"{Year}:{Sector}:{Code}:{RowNumber}";
}

public sealed class StockMovementTotals
{
    public decimal InitialQuantity { get; set; }

    public decimal InitialValue { get; set; }

    public decimal LoadQuantity { get; set; }

    public decimal LoadValue { get; set; }

    public decimal UnloadQuantity { get; set; }

    public decimal UnloadValue { get; set; }

    public decimal StockQuantity { get; set; }

    public decimal StockValue { get; set; }
}

public sealed record StockMovementSubjectOption(int Code, string Name);
