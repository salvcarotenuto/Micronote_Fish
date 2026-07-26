namespace MicronoteFood.Web.Models;

public sealed class ArticleAccountPageModel
{
    public int Year { get; set; }

    public int Month { get; set; }

    public string ArticleCode { get; set; } = "";

    public string ArticleDescription { get; set; } = "";

    public string? SelectedKey { get; set; }

    public IReadOnlyList<int> Years { get; set; } = [];

    public IReadOnlyList<ArticleAccountMovementItem> Movements { get; set; } = [];

    public ArticleAccountTotals Totals { get; set; } = new();
}

public sealed record ArticleAccountMovementItem(
    int Year,
    int Sector,
    int Code,
    int RowNumber,
    DateOnly MovementDate,
    string DocumentNumber,
    string MovementType,
    decimal Quantity,
    decimal Price,
    decimal Amount,
    string SubjectType,
    int SubjectCode,
    string SubjectName,
    int StockLoadId,
    int SaleId)
{
    public string Key => $"{Year}:{Sector}:{Code}:{RowNumber}";

    public decimal LoadQuantity => MovementType == "C" ? Quantity : 0;

    public decimal LoadPrice => MovementType == "C" ? Price : 0;

    public decimal LoadAmount => MovementType == "C" ? Amount : 0;

    public decimal UnloadQuantity => MovementType == "S" ? Quantity : 0;

    public decimal UnloadPrice => MovementType == "S" ? Price : 0;

    public decimal UnloadAmount => MovementType == "S" ? Amount : 0;
}

public sealed class ArticleAccountTotals
{
    public decimal InitialQuantity { get; set; }

    public decimal InitialValue { get; set; }

    public decimal LoadQuantity { get; set; }

    public decimal LoadAverageCost { get; set; }

    public decimal LoadValue { get; set; }

    public decimal UnloadQuantity { get; set; }

    public decimal UnloadAveragePrice { get; set; }

    public decimal UnloadValue { get; set; }

    public decimal FinalQuantity { get; set; }

    public decimal FinalValue { get; set; }
}
