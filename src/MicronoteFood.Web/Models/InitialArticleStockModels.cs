namespace MicronoteFood.Web.Models;

public sealed record InitialArticleStockRow(
    string Code,
    string Description,
    string UnitMeasure,
    string Category,
    string Group,
    int Packages,
    decimal Quantity);

public sealed record InitialArticleStockSaveRow(
    string Code,
    int Packages,
    decimal Quantity);

public sealed record InitialArticleStockData(
    DateOnly? InventoryDate,
    IReadOnlyList<InitialArticleStockRow> Rows);
