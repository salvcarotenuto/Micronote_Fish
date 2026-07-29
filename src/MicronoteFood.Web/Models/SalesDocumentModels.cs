namespace MicronoteFood.Web.Models;

public sealed class SalesDocumentPageData
{
    public int? Id { get; init; }
    public int Year { get; init; }
    public int Code { get; init; }
    public DateOnly DocumentDate { get; init; }
    public int CustomerCode { get; init; }
    public string CustomerName { get; init; } = "";
    public int StoreCode { get; init; }
    public decimal Discount { get; init; }
    public decimal Paid { get; init; }
    public IReadOnlyList<SalesDocumentRow> Rows { get; init; } = [];
}

public sealed record SalesDocumentRow(
    int RowNumber,
    string ArticleCode,
    string Description,
    string Unit,
    int Packages,
    decimal Tare,
    decimal Quantity,
    decimal Price,
    decimal VatRate,
    decimal Amount);

public sealed class SalesDocumentSaveModel
{
    public int? Id { get; set; }
    public int CustomerCode { get; set; }
    public int StoreCode { get; set; }
    public DateOnly DocumentDate { get; set; }
    public decimal Discount { get; set; }
    public decimal Paid { get; set; }
    public List<SalesDocumentSaveRow> Rows { get; set; } = [];
}

public sealed class SalesDocumentSaveRow
{
    public string ArticleCode { get; set; } = "";
    public string Unit { get; set; } = "";
    public int Packages { get; set; }
    public decimal Tare { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal VatRate { get; set; }
    public decimal Amount { get; set; }
}

public sealed record SalesDocumentSaveResult(int Id, int Code);
