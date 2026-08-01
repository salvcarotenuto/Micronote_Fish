namespace MicronoteFood.Web.Models;

public sealed class CustomerNotePageModel
{
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public int? StoreCode { get; set; }
    public int? SelectedCustomerCode { get; set; }
    public IReadOnlyList<CustomerNoteStoreOption> Stores { get; set; } = [];
    public IReadOnlyList<CustomerNoteSummaryRow> Customers { get; set; } = [];
    public IReadOnlyList<CustomerNoteDetailRow> Details { get; set; } = [];
    public CustomerNoteSummaryRow? SelectedCustomer =>
        Customers.FirstOrDefault(row => row.CustomerCode == SelectedCustomerCode);
}

public sealed record CustomerNoteStoreOption(int Code, string Name);

public sealed record CustomerNoteSummaryRow(
    int CustomerCode,
    string CustomerName,
    decimal Merchandise,
    decimal Vat,
    decimal Total,
    decimal PreviousBalance,
    decimal Paid,
    decimal CashAllowance,
    decimal UpdatedBalance,
    decimal? CreditLimit);

public sealed record CustomerNoteDetailRow(
    int SaleId,
    DateOnly DocumentDate,
    string ArticleCode,
    string ArticleDescription,
    decimal Quantity,
    decimal Price,
    decimal VatRate,
    decimal Amount);
