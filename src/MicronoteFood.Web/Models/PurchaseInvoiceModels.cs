namespace MicronoteFood.Web.Models;

public sealed class PurchaseInvoiceListPageModel
{
    public int Year { get; set; }

    public int? Month { get; set; }

    public int? CauseCode { get; set; }

    public int? SupplierCode { get; set; }

    public string SupplierName { get; set; } = "";

    public int? StoreCode { get; set; }

    public int? ContraAccountCode { get; set; }

    public bool ShowDueDates { get; set; }

    public int? SelectedId { get; set; }

    public IReadOnlyList<PurchaseInvoiceListItem> Invoices { get; set; } = [];

    public PurchaseInvoiceTotals Totals { get; set; } = PurchaseInvoiceTotals.Empty;

    public IReadOnlyList<int> Years { get; set; } = [];

    public IReadOnlyList<PurchaseInvoiceMonthOption> Months { get; set; } = [];

    public IReadOnlyList<PurchaseInvoiceCauseOption> Causes { get; set; } = [];

    public IReadOnlyList<PurchaseInvoiceStoreOption> Stores { get; set; } = [];

    public IReadOnlyList<PurchaseInvoiceAccountOption> ContraAccounts { get; set; } = [];
}

public sealed record PurchaseInvoiceListItem(
    int Id,
    int Year,
    int Code,
    string DocumentNumber,
    DateOnly DocumentDate,
    int CauseCode,
    string CauseDescription,
    int SupplierCode,
    string SupplierName,
    int ContraAccountCode,
    string ContraAccountDescription,
    decimal Net,
    decimal Vat,
    decimal Total,
    string ElectronicInvoiceName);

public sealed record PurchaseInvoiceTotals(
    decimal Net,
    decimal Vat,
    decimal Total)
{
    public static PurchaseInvoiceTotals Empty { get; } = new(0, 0, 0);
}

public sealed record PurchaseInvoiceMonthOption(
    int Value,
    string Description);

public sealed record PurchaseInvoiceCauseOption(
    int Code,
    string Description);

public sealed record PurchaseInvoiceStoreOption(
    int Code,
    string Description);

public sealed record PurchaseInvoiceAccountOption(
    int Code,
    string Description);

public sealed class PurchaseInvoiceEditPageModel
{
    public int? Id { get; set; }

    public int Year { get; set; }

    public int Code { get; set; }

    public DateOnly DocumentDate { get; set; }

    public int CauseCode { get; set; }

    public string DocumentNumber { get; set; } = "";

    public int SupplierCode { get; set; }

    public string SupplierName { get; set; } = "";

    public int ContraAccountCode { get; set; }

    public int StoreCode { get; set; }

    public int PaymentCode { get; set; }

    public int BankCode { get; set; }

    public decimal TaxableTotal { get; set; }

    public decimal VatTotal { get; set; }

    public decimal Total { get; set; }

    public string Notes { get; set; } = "";

    public string ElectronicInvoiceFileName { get; set; } = "";

    public IReadOnlyList<PurchaseInvoiceVatSaveRow> VatRows { get; set; } = [];

    public IReadOnlyList<PurchaseInvoiceDueDateSaveRow> DueRows { get; set; } = [];

    public IReadOnlyList<PurchaseInvoiceCauseOption> Causes { get; set; } = [];

    public IReadOnlyList<PurchaseInvoiceAccountOption> ContraAccounts { get; set; } = [];

    public IReadOnlyList<PurchaseInvoiceStoreOption> Stores { get; set; } = [];

    public IReadOnlyList<PurchaseInvoicePaymentOption> Payments { get; set; } = [];

    public IReadOnlyList<PurchaseInvoiceBankOption> Banks { get; set; } = [];
}

public sealed record PurchaseInvoicePaymentOption(
    int Code,
    string Description);

public sealed record PurchaseInvoiceBankOption(
    int Code,
    string Description);

public sealed class PurchaseInvoiceSaveCommand
{
    public int? Id { get; set; }

    public int Year { get; set; }

    public int CauseCode { get; set; }

    public string DocumentNumber { get; set; } = "";

    public DateOnly? DocumentDate { get; set; }

    public int SupplierCode { get; set; }

    public int ContraAccountCode { get; set; }

    public int StoreCode { get; set; }

    public int PaymentCode { get; set; }

    public int BankCode { get; set; }

    public decimal Total { get; set; }

    public string Notes { get; set; } = "";

    public string ElectronicInvoiceFileName { get; set; } = "";

    public bool ConfirmOverwrite { get; set; }

    public bool ConfirmDueDateMismatch { get; set; }

    public IReadOnlyList<PurchaseInvoiceVatSaveRow> VatRows { get; set; } = [];

    public IReadOnlyList<PurchaseInvoiceDueDateSaveRow> DueRows { get; set; } = [];
}

public sealed class PurchaseInvoiceVatSaveRow
{
    public decimal Rate { get; set; }

    public decimal Taxable { get; set; }

    public decimal Tax { get; set; }
}

public sealed class PurchaseInvoiceDueDateSaveRow
{
    public int Number { get; set; }

    public decimal Amount { get; set; }

    public DateOnly? Date { get; set; }

    public bool Paid { get; set; }
}

public sealed record PurchaseInvoiceSaveResult(
    bool Success,
    bool RequiresOverwriteConfirmation,
    string Message,
    int? Id = null,
    int? Year = null,
    int? Code = null);
