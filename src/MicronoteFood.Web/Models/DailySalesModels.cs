namespace MicronoteFood.Web.Models;

public sealed class DailySalesPageModel
{
    public DateOnly MovementDate { get; set; }
    public int Code { get; set; }
    public decimal VatRate { get; set; }
    public IReadOnlyList<DailySalesRow> Rows { get; set; } = [];
    public DailySalesTotals Totals { get; set; } = DailySalesTotals.Empty;
}

public sealed record DailySalesRow(
    int StoreCode, string StoreName, decimal TaxableGross, decimal Exempt,
    decimal Net, decimal Vat, decimal Total, decimal Cash, decimal Card,
    decimal Tickets, decimal Checks, decimal Other, decimal Suspended, decimal Losses);

public sealed record DailySalesTotals(
    decimal TaxableGross, decimal Exempt, decimal Net, decimal Vat, decimal Total,
    decimal Cash, decimal Card, decimal Tickets, decimal Checks, decimal Other,
    decimal Suspended, decimal Losses)
{
    public decimal TotalCash => Cash + Other;
    public decimal NetReceipts => TotalCash + Card + Tickets + Checks;
    public decimal Balance => NetReceipts + Suspended + Losses - Total;
    public static DailySalesTotals Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
