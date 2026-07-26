namespace MicronoteFood.Web.Models;

public sealed class AccountingMovementSummaryPageModel
{
    public int Year { get; set; }
    public bool IsLoaded { get; set; }
    public IReadOnlyList<int> Years { get; set; } = [];
    public IReadOnlyList<AccountingMovementVatSummaryRow> Purchases { get; set; } = [];
    public IReadOnlyList<AccountingMovementAmountSummaryRow> Costs { get; set; } = [];
    public IReadOnlyList<AccountingMovementVatSummaryRow> Revenues { get; set; } = [];
    public IReadOnlyList<AccountingMovementAmountSummaryRow> Collections { get; set; } = [];
    public decimal TotalPurchases { get; set; }
    public decimal TotalCosts { get; set; }
    public decimal TotalRevenues { get; set; }
    public decimal TotalCollections { get; set; }
    public decimal TotalPayments { get; set; }
    public int RecordCount => Purchases.Count + Costs.Count + Revenues.Count + Collections.Count;
}

public sealed record AccountingMovementVatSummaryRow(
    int? Code,
    string Description,
    decimal Taxable,
    decimal Vat,
    decimal Total,
    decimal Share,
    bool IsTotal = false);

public sealed record AccountingMovementAmountSummaryRow(
    int? Code,
    string Description,
    decimal Amount,
    decimal Share,
    bool IsTotal = false);
