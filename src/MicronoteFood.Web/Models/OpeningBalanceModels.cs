namespace MicronoteFood.Web.Models;

public sealed record OpeningBalanceRow(
    int MasterCode,
    string MasterDescription,
    int AccountCode,
    string AccountDescription,
    decimal Debit,
    decimal Credit);

public sealed record OpeningBalanceSaveRow(
    int AccountCode,
    decimal Debit,
    decimal Credit);

public sealed record OpeningBalanceList(
    int Year,
    IReadOnlyList<int> Years,
    DateOnly MovementDate,
    IReadOnlyList<OpeningBalanceRow> Rows)
{
    public decimal DebitTotal => Rows.Sum(row => row.Debit);

    public decimal CreditTotal => Rows.Sum(row => row.Credit);
}
