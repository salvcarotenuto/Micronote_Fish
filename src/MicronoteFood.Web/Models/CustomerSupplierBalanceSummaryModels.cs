namespace MicronoteFood.Web.Models;

public sealed class CustomerSupplierBalanceSummaryPageModel
{
    public int Year { get; set; }
    public bool ShowZero { get; set; }
    public int? StoreCode { get; set; }
    public IReadOnlyList<int> Years { get; set; } = [];
    public IReadOnlyList<CustomerSupplierBalanceStoreOption> Stores { get; set; } = [];
    public IReadOnlyList<CustomerSupplierBalanceSummaryRow> Customers { get; set; } = [];
    public IReadOnlyList<CustomerSupplierBalanceSummaryRow> Suppliers { get; set; } = [];
    public CustomerSupplierBalanceTotals CustomerTotals => Totals(Customers);
    public CustomerSupplierBalanceTotals SupplierTotals => Totals(Suppliers);

    private static CustomerSupplierBalanceTotals Totals(IReadOnlyList<CustomerSupplierBalanceSummaryRow> rows) =>
        new(rows.Sum(row => row.InitialBalance), rows.Sum(row => row.Operations),
            rows.Sum(row => row.Settlements), rows.Sum(row => row.Balance));
}

public sealed record CustomerSupplierBalanceSummaryRow(
    string Type,
    int Code,
    string Name,
    decimal InitialBalance,
    decimal Operations,
    decimal Settlements,
    decimal Balance);

public sealed record CustomerSupplierBalanceTotals(
    decimal InitialBalance,
    decimal Operations,
    decimal Settlements,
    decimal Balance);

public sealed record CustomerSupplierBalanceStoreOption(int Code, string Name);
