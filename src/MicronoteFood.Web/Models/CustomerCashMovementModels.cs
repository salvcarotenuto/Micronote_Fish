namespace MicronoteFood.Web.Models;

public sealed class CustomerCashMovementEditModel
{
    public int Id { get; set; }
    public bool IsNew { get; set; } = true;
    public int Year { get; set; }
    public int Sector { get; set; } = 40;
    public DateOnly MovementDate { get; set; }
    public int CustomerCode { get; set; }
    public string CustomerName { get; set; } = "";
    public int CustomerStoreCode { get; set; }
    public int CauseCode { get; set; }
    public decimal Amount { get; set; }
    public int PaymentMethod { get; set; }
    public string DocumentType { get; set; } = "";
    public int? DocumentId { get; set; }
    public int? DocumentNumber { get; set; }
    public DateOnly? DocumentDate { get; set; }
    public string Description { get; set; } = "";
}

public sealed record CustomerCashMovementCauseOption(int Code, string Description);
public sealed record CustomerCashMovementStoreOption(int Code, string Name);

public sealed class CustomerCashMovementMaskModel
{
    public IReadOnlyList<CustomerCashMovementCauseOption> Causes { get; set; } = [];
    public IReadOnlyList<CustomerCashMovementStoreOption> Stores { get; set; } = [];
}

public sealed record CustomerCashMovementDocumentRow(
    int Id, int Year, int Code, int Number, DateOnly Date, decimal Total);

public sealed record CustomerCashMovementSaveResult(bool Success, int Id, string Error = "");
