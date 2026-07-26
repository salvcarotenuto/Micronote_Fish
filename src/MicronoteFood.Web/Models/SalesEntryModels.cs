using System.ComponentModel.DataAnnotations;

namespace MicronoteFood.Web.Models;

public sealed class SalesEntryPageModel
{
    public int? SaleId { get; set; }

    public string? ReturnTo { get; set; }

    public int Year { get; set; }

    [Display(Name = "Progressivo")]
    public int Code { get; set; }

    [Display(Name = "Data movimento")]
    public DateOnly MovementDate { get; set; }

    [Display(Name = "Ultima registrazione")]
    public DateOnly? LastRegistrationDate { get; set; }

    public decimal SalesVatRate { get; set; }

    public IReadOnlyList<SalesEntryStoreRow> Stores { get; set; } = [];

    public IReadOnlyList<SalesEntrySaveRow> SavedRows { get; set; } = [];
}

public sealed record SalesEntryStoreRow(
    int Code,
    string Name);

public sealed class SalesEntrySaveModel
{
    public int? SaleId { get; set; }

    public string? ReturnTo { get; set; }

    public int Year { get; set; }

    public int Code { get; set; }

    public DateOnly MovementDate { get; set; }

    public decimal SalesVatRate { get; set; }

    public bool ConfirmOverwrite { get; set; }

    public List<SalesEntrySaveRow> Rows { get; set; } = [];
}

public sealed class SalesEntrySaveRow
{
    public int StoreCode { get; set; }

    public string StoreName { get; set; } = "";

    public string? Taxable { get; set; }

    public string? Exempt { get; set; }

    public string? Card { get; set; }

    public string? Tickets { get; set; }

    public string? Checks { get; set; }

    public string? Other { get; set; }

    public string? Suspended { get; set; }

    public string? Losses { get; set; }
}

public sealed record SalesEntrySaveCommand(
    int? SaleId,
    int Year,
    int Code,
    DateOnly MovementDate,
    decimal SalesVatRate,
    IReadOnlyList<SalesEntrySaveCommandRow> Rows);

public sealed record SalesEntrySaveCommandRow(
    int StoreCode,
    decimal Taxable,
    decimal Exempt,
    decimal Net,
    decimal Vat,
    decimal Total,
    decimal Cash,
    decimal Card,
    decimal Tickets,
    decimal Checks,
    decimal Other,
    decimal Suspended,
    decimal Losses);

public sealed record SalesEntrySaveResult(
    bool Saved,
    int Code,
    bool RequiresOverwriteConfirmation,
    DateOnly? ExistingDate);

public sealed class SalesHistoryPageModel
{
    public int Year { get; set; }

    public int Month { get; set; }

    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    public IReadOnlyList<int> Years { get; set; } = [];

    public IReadOnlyList<SalesHistoryListItem> Sales { get; set; } = [];

    public SalesHistoryTotals Totals { get; set; } = SalesHistoryTotals.Empty;
}

public sealed record SalesHistoryListItem(
    int Id,
    int? AccountingMovementId,
    int Year,
    int Code,
    DateOnly MovementDate,
    decimal Net,
    decimal NonTaxable,
    decimal Vat,
    decimal Total,
    decimal Cash,
    decimal Card,
    decimal Tickets,
    decimal Checks,
    decimal Other,
    decimal Suspended,
    decimal Losses);

public sealed record SalesHistoryDetailItem(
    int StoreCode,
    string StoreName,
    decimal Net,
    decimal NonTaxable,
    decimal Vat,
    decimal Total,
    decimal Cash,
    decimal Card,
    decimal Tickets,
    decimal Checks,
    decimal Other,
    decimal Suspended,
    decimal Losses);

public sealed record SalesHistoryTotals(
    decimal Net,
    decimal NonTaxable,
    decimal Vat,
    decimal Total,
    decimal Cash,
    decimal Card,
    decimal Tickets,
    decimal Checks,
    decimal Other,
    decimal Suspended,
    decimal Losses)
{
    public static SalesHistoryTotals Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
