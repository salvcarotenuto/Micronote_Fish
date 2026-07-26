using System.Globalization;
using MicronoteFood.Web.Models;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace MicronoteFood.Web.Services;

public sealed class StoreMovementSummaryPdfService
{
    static StoreMovementSummaryPdfService() => GlobalFontSettings.UseWindowsFontsUnderWindows = true;

    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");
    private const double Margin = 28;
    private const double RowHeight = 14;

    public byte[] Create(StoreMovementSummaryPageModel report, DateTime generatedAt)
    {
        using var document = new PdfDocument();
        document.Info.Title = $"Riepilogo movimenti per punto vendita {report.Year}";
        document.Info.Creator = "Micronote Food";
        AddSection(document, report, report.Revenues, false, generatedAt);
        AddSection(document, report, report.Costs, true, generatedAt);
        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static void AddSection(PdfDocument document, StoreMovementSummaryPageModel report,
        StoreMovementSummaryGrid grid, bool includeCommon, DateTime generatedAt)
    {
        var rows = grid.Rows.Where(row => !row.IsSeparator).ToArray();
        const int rowsPerPage = 31;
        var pageCount = Math.Max(1, (int)Math.Ceiling(rows.Length / (double)rowsPerPage));
        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            page.Orientation = PdfSharp.PageOrientation.Landscape;
            using var graphics = XGraphics.FromPdfPage(page);
            var titleFont = new XFont("Arial", 9, XFontStyleEx.Bold);
            var font = new XFont("Arial", 6.5, XFontStyleEx.Regular);
            var bold = new XFont("Arial", 6.5, XFontStyleEx.Bold);
            graphics.DrawString($"RIEPILOGO MOVIMENTI CONTABILI PER PUNTO VENDITA  -  Esercizio contabile: {report.Year}",
                titleFont, XBrushes.Black, new XRect(Margin + 60, Margin, 560, 16), XStringFormats.TopLeft);
            graphics.DrawString($"data di stampa: {generatedAt:dd-MM-yyyy}", font, XBrushes.Black,
                new XRect(page.Width.Point - Margin - 145, Margin, 145, 14), XStringFormats.TopRight);
            graphics.DrawString(grid.Title, bold, XBrushes.Black,
                new XRect(Margin + 60, Margin + 21, 400, 14), XStringFormats.TopLeft);

            var columns = BuildColumns(report.Stores, includeCommon, page.Width.Point - Margin * 2);
            var y = Margin + 48;
            DrawHeader(graphics, columns, y, bold);
            y += 21;
            foreach (var row in rows.Skip(pageIndex * rowsPerPage).Take(rowsPerPage))
            {
                DrawRow(graphics, columns, row, report.Stores.Count, includeCommon, y, row.IsTotal ? bold : font);
                if (row.IsTotal)
                    graphics.DrawLine(XPens.Gray, Margin, y - 2, page.Width.Point - Margin, y - 2);
                y += RowHeight;
            }
            graphics.DrawLine(XPens.Gray, Margin, y + 1, page.Width.Point - Margin, y + 1);
            graphics.DrawString($"Pagina {pageIndex + 1} di {pageCount}", font, XBrushes.DimGray,
                new XRect(Margin, page.Height.Point - Margin, page.Width.Point - Margin * 2, 12), XStringFormats.TopRight);
        }
    }

    private static List<(string Header, double Width, bool Right)> BuildColumns(
        IReadOnlyList<StoreMovementSummaryStore> stores, bool includeCommon, double available)
    {
        var result = new List<(string, double, bool)> { ("Conto", 35, false), (includeCommon ? "Costi" : "Ricavi", 125, false), (includeCommon ? "Totale" : "", 58, true), ("Quota", 43, true) };
        foreach (var store in stores)
            result.AddRange([(store.Name, 64, true), ("Quota cst.", 43, true), ("Quota ric.", 43, true)]);
        if (includeCommon)
            result.AddRange([("Spese comuni", 64, true), ("Quota cst.", 43, true), ("Quota ric.", 43, true)]);
        var scale = available / result.Sum(column => column.Item2);
        return result.Select(column => (column.Item1, column.Item2 * scale, column.Item3)).ToList();
    }

    private static void DrawHeader(XGraphics graphics, List<(string Header, double Width, bool Right)> columns, double y, XFont font)
    {
        var x = Margin;
        foreach (var column in columns)
        {
            graphics.DrawRectangle(new XSolidBrush(XColor.FromArgb(232, 232, 232)), x, y, column.Width, 18);
            graphics.DrawString(column.Header, font, XBrushes.Black, new XRect(x + 2, y, column.Width - 4, 18),
                column.Right ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
            x += column.Width;
        }
        graphics.DrawLine(XPens.Gray, Margin, y + 18, x, y + 18);
    }

    private static void DrawRow(XGraphics graphics, List<(string Header, double Width, bool Right)> columns,
        StoreMovementSummaryRow row, int storeCount, bool includeCommon, double y, XFont font)
    {
        var values = new List<string> { row.AccountCode?.ToString("000") ?? "", row.Description, Money(row.Total), Percent(row.Share) };
        for (var index = 0; index < storeCount; index++)
        {
            var value = index < row.StoreValues.Count ? row.StoreValues[index] : new(0, 0, 0);
            values.AddRange([Money(value.Amount), Percent(value.CostShare), Percent(value.RevenueShare)]);
        }
        if (includeCommon)
        {
            var value = row.CommonValue ?? new(0, 0, 0);
            values.AddRange([Money(value.Amount), Percent(value.CostShare), Percent(value.RevenueShare)]);
        }
        var x = Margin;
        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            graphics.DrawString(values[index], font, XBrushes.Black, new XRect(x + 2, y, column.Width - 4, RowHeight),
                column.Right ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
            x += column.Width;
        }
    }

    private static string Money(decimal value) => value == 0 ? "" : value.ToString("N2", Italian);
    private static string Percent(decimal value) => value == 0 ? "" : value.ToString("N2", Italian) + " %";
}
