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

    public int CustomerCode { get; set; }

    public string CustomerName { get; set; } = "";

    public int StoreCode { get; set; }

    public IReadOnlyList<int> Years { get; set; } = [];

    public IReadOnlyList<SalesEntryStoreRow> Stores { get; set; } = [];

    public IReadOnlyList<SalesHistoryListItem> Sales { get; set; } = [];

    public SalesHistoryTotals Totals { get; set; } = SalesHistoryTotals.Empty;
}

public sealed record SalesHistoryListItem(
    int Id,
    int Year,
    int Code,
    int DocumentNumber,
    DateOnly? DocumentDate,
    int CustomerCode,
    string CustomerName,
    decimal Merchandise,
    decimal Vat,
    decimal Total,
    decimal Discount,
    int StoreCode);

public sealed record SalesHistoryDetailItem(
    int RowNumber,
    string ArticleCode,
    string Description,
    string Unit,
    int Packages,
    decimal Tare,
    decimal Quantity,
    decimal Price,
    decimal VatRate,
    decimal VatPrice,
    decimal Amount);

public sealed record SalesHistoryTotals(
    decimal Merchandise,
    decimal Vat,
    decimal Total,
    decimal Discount)
{
    public static SalesHistoryTotals Empty { get; } = new(0, 0, 0, 0);
}
