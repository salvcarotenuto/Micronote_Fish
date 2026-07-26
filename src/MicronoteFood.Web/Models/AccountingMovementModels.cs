namespace MicronoteFood.Web.Models;

public sealed class AccountingMovementListPageModel
{
    public int Year { get; set; }

    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    public int? CauseCode { get; set; }

    public string? MovementType { get; set; }

    public int? SupplierCode { get; set; }

    public string SupplierName { get; set; } = "";

    public int? SelectedId { get; set; }

    public IReadOnlyList<AccountingMovementListItem> Movements { get; set; } = [];

    public IReadOnlyList<AccountingMovementCauseOption> Causes { get; set; } = [];

    public IReadOnlyList<AccountingMovementTypeOption> MovementTypes { get; set; } = [];

    public IReadOnlyList<AccountingMovementSupplierOption> Suppliers { get; set; } = [];
}

public sealed record AccountingMovementListItem(
    int Id,
    int Year,
    int Sector,
    int Code,
    string SourceDocument,
    DateOnly MovementDate,
    string DocumentNumber,
    int CauseCode,
    string CauseDescription,
    string MovementType,
    decimal Amount,
    string SubjectType,
    int SubjectCode,
    string SubjectName,
    string Notes,
    bool PrintEnabled);

public sealed record AccountingMovementCauseOption(
    int Code,
    string Description);

public sealed record AccountingMovementTypeOption(
    string Code,
    string Description);

public sealed record AccountingMovementSupplierOption(
    int Code,
    string Name);

public sealed class AccountingMovementEditModel
{
    public bool IsNew { get; set; }

    public int? Id { get; set; }

    public int Year { get; set; }

    public int Sector { get; set; } = 40;

    public int Code { get; set; }

    public DateOnly MovementDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public int? CauseCode { get; set; }

    public string CauseDescription { get; set; } = "";

    public string SubjectType { get; set; } = "";

    public int? SubjectCode { get; set; }

    public string SubjectName { get; set; } = "";

    public string DocumentNumber { get; set; } = "";

    public int? LinkedInvoiceDocumentId { get; set; }

    public int? LinkedInvoiceDocumentSector { get; set; }

    public string LinkedInvoiceDocumentNumber { get; set; } = "";

    public DateOnly? LinkedInvoiceDocumentDate { get; set; }

    public int? LinkedDueDateDocumentId { get; set; }

    public int? LinkedDueDateDocumentSector { get; set; }

    public string LinkedDueDateDocumentNumber { get; set; } = "";

    public DateOnly? LinkedDueDateDocumentDate { get; set; }

    public int? StoreCode { get; set; }

    public string Notes { get; set; } = "";

    public decimal Amount { get; set; }

    public List<AccountingMovementLineEditModel> DebitLines { get; set; } =
        Enumerable.Range(1, 6).Select(index => new AccountingMovementLineEditModel { RowNumber = index, Sign = "D" }).ToList();

    public List<AccountingMovementLineEditModel> CreditLines { get; set; } =
        Enumerable.Range(1, 6).Select(index => new AccountingMovementLineEditModel { RowNumber = index, Sign = "A" }).ToList();
}

public sealed class AccountingMovementLineEditModel
{
    public int RowNumber { get; set; }

    public string Sign { get; set; } = "";

    public int? AccountCode { get; set; }

    public decimal Amount { get; set; }
}

public sealed record AccountingMovementCauseTemplate(
    int Code,
    string Description,
    string MovementType,
    string Subject,
    int? Debit1,
    int? Debit2,
    int? Debit3,
    int? Debit4,
    int? Debit5,
    int? Debit6,
    int? Credit1,
    int? Credit2,
    int? Credit3,
    int? Credit4,
    int? Credit5,
    int? Credit6,
    bool Invoice,
    bool DueDate);

public sealed record AccountingMovementStoreOption(
    int Code,
    string Name);

public sealed record AccountingLinkedDocumentOption(
    int Id,
    int Year,
    int Sector,
    int Code,
    string Number,
    DateOnly? Date,
    decimal Amount);


public sealed class BankMovementListPageModel
{
    public int Year { get; set; }

    public int Month { get; set; }

    public int? BankCode { get; set; }

    public int? CauseCode { get; set; }

    public int? SelectedId { get; set; }

    public IReadOnlyList<int> Years { get; set; } = [];

    public IReadOnlyList<MonthOption> Months { get; set; } = [];

    public IReadOnlyList<BankMovementBankOption> Banks { get; set; } = [];

    public IReadOnlyList<AccountingMovementCauseOption> Causes { get; set; } = [];

    public IReadOnlyList<BankMovementListItem> Movements { get; set; } = [];

    public decimal Total { get; set; }
}

public sealed record BankMovementListItem(
    int Id,
    int Year,
    int Code,
    DateOnly MovementDate,
    int CauseCode,
    string CauseDescription,
    int BankCode,
    string BankName,
    decimal Amount,
    string Title);

public sealed record BankMovementBankOption(
    int Code,
    string Description);

public sealed record MonthOption(
    int Code,
    string Description);

public sealed class AccountingArticleDetailModel
{
    public int Id { get; set; }

    public int Year { get; set; }

    public int Code { get; set; }

    public DateOnly MovementDate { get; set; }

    public int CauseCode { get; set; }

    public string CauseDescription { get; set; } = "";

    public string SubjectLabel { get; set; } = "";

    public int? SubjectCode { get; set; }

    public string SubjectName { get; set; } = "";

    public string DocumentNumber { get; set; } = "";

    public decimal Amount { get; set; }

    public IReadOnlyList<AccountingArticleDetailLine> DebitLines { get; set; } = [];

    public IReadOnlyList<AccountingArticleDetailLine> CreditLines { get; set; } = [];

    public decimal DebitTotal => DebitLines.Sum(line => line.Amount);

    public decimal CreditTotal => CreditLines.Sum(line => line.Amount);
}

public sealed record AccountingArticleDetailLine(
    int AccountCode,
    string AccountDescription,
    decimal Amount);
