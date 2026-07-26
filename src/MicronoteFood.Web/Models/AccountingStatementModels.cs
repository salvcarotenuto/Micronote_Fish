namespace MicronoteFood.Web.Models;

public sealed class AccountingStatementPageModel
{
    public string StatementMode { get; set; } = "account";
    public string PageTitle { get; set; } = "Scheda contabile";
    public string PageDescription { get; set; } = "Visualizzazione e stampa scheda contabile";
    public int? AccountCode { get; set; }
    public string AccountDescription { get; set; } = "";
    public string PartyType { get; set; } = "";
    public int? PartyCode { get; set; }
    public string PartyName { get; set; } = "";
    public AccountingStatementPartyLookupRow? Party { get; set; }
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public bool IsLoaded { get; set; }
    public IReadOnlyList<AccountingStatementRow> Rows { get; set; } = [];
    public IReadOnlyList<AccountingStatementAccountLookupRow> Accounts { get; set; } = [];
    public IReadOnlyList<AccountingStatementPartyLookupRow> Parties { get; set; } = [];
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal Balance { get; set; }
    public string BalanceSign => Balance < 0 ? "A" : "D";
    public int RecordCount => Rows.Count;
}

public sealed record AccountingStatementRow(
    int? Id,
    int? Year,
    int? Sector,
    int? Code,
    DateOnly? MovementDate,
    string DocumentNumber,
    int? CauseCode,
    string CauseDescription,
    int? CounterpartCode,
    string CounterpartDescription,
    decimal Debit,
    decimal Credit,
    decimal Balance,
    string Sign);

public sealed record AccountingStatementAccountLookupRow(
    int MasterCode,
    string MasterDescription,
    int AccountCode,
    string AccountDescription,
    string TypeCode,
    string TypeDescription);

public sealed record AccountingStatementPartyLookupRow(
    string Type,
    int Code,
    string Name,
    string Address,
    string City,
    string PostalCode,
    string Province,
    string TaxCode,
    string VatNumber);
