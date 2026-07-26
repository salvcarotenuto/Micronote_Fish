namespace MicronoteFood.Web.Models;

public sealed class StoreMovementSummaryPageModel
{
    public int Year { get; set; }

    public bool IsLoaded { get; set; }

    public IReadOnlyList<int> Years { get; set; } = [];

    public IReadOnlyList<StoreMovementSummaryStore> Stores { get; set; } = [];

    public StoreMovementSummaryGrid Revenues { get; set; } = new("Ricavi del periodo", []);

    public StoreMovementSummaryGrid Costs { get; set; } = new("Costi del periodo", []);

    public int RecordCount => Costs.Rows.Count(row => !row.IsSeparator);
}

public sealed record StoreMovementSummaryStore(int Code, string Name);

public sealed record StoreMovementSummaryGrid(
    string Title,
    IReadOnlyList<StoreMovementSummaryRow> Rows);

public sealed record StoreMovementSummaryRow(
    int? AccountCode,
    string Description,
    decimal Total,
    decimal Share,
    IReadOnlyList<StoreMovementSummaryStoreValue> StoreValues,
    StoreMovementSummaryStoreValue? CommonValue = null,
    bool IsTotal = false,
    bool IsSeparator = false);

public sealed record StoreMovementSummaryStoreValue(
    decimal Amount,
    decimal CostShare,
    decimal RevenueShare);

public sealed record StoreSummaryGridViewModel(
    StoreMovementSummaryGrid Grid,
    IReadOnlyList<StoreMovementSummaryStore> Stores,
    bool IncludeCommon);

public sealed record StoreMovementSummaryPrintRequest(int Year);
