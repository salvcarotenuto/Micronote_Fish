namespace MicronoteFood.Web.Models;

public sealed record InitialArticleStockRow(
    string Code,
    string Description,
    string UnitMeasure,
    string Category,
    string Group,
    decimal Quantity);

public sealed record InitialArticleStockSaveRow(
    string Code,
    decimal Quantity);
