using System.Globalization;
using MicronoteFood.Web.Models;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace MicronoteFood.Web.Services;

public sealed class TrialBalancePdfService
{
    static TrialBalancePdfService()
    {
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");
    private const double Margin = 34;
    private const double RowHeight = 16;

    public byte[] Create(TrialBalancePageModel report, DateTime generatedAt)
    {
        using var document = new PdfDocument();
        document.Info.Title = $"Bilancio di verifica {report.Year}";
        document.Info.Subject = "Bilancio di verifica";
        document.Info.Creator = "Micronote Food";

        AddEquityPages(document, report, generatedAt);
        AddIncomePages(document, report, generatedAt);

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static void AddEquityPages(PdfDocument document, TrialBalancePageModel report, DateTime generatedAt)
    {
        const int rowsPerPage = 35;
        var pageCount = Math.Max(1, (int)Math.Ceiling(report.EquityRows.Count / (double)rowsPerPage));
        for (var index = 0; index < pageCount; index++)
        {
            var rows = report.EquityRows.Skip(index * rowsPerPage).Take(rowsPerPage).ToArray();
            var (graphics, page, y, rowFont, boldFont) = BeginPage(document, report.Year, "Stato patrimoniale", generatedAt);
            using (graphics)
            {
                DrawHeader(graphics, y, [("Conto", 50d), ("Descrizione", 260d), ("Attività", 105d), ("Passività", 105d)], boldFont);
                y += 22;
                foreach (var row in rows)
                {
                    DrawCells(graphics, y,
                        [(row.AccountCode.ToString("000"), 50d, false), (row.Description, 260d, false),
                         (Money(row.Assets), 105d, true), (Money(row.Liabilities), 105d, true)], rowFont);
                    y += RowHeight;
                }
                if (index == pageCount - 1)
                {
                    DrawTotalLine(graphics, y, [("", 50d, false), ("TOTALI", 260d, false),
                        (Money(report.TotalAssets), 105d, true), (Money(report.TotalLiabilities), 105d, true)], boldFont);
                }
                DrawPageNumber(graphics, page, index + 1, pageCount);
            }
        }
    }

    private static void AddIncomePages(PdfDocument document, TrialBalancePageModel report, DateTime generatedAt)
    {
        const int rowsPerPage = 35;
        var pageCount = Math.Max(1, (int)Math.Ceiling(report.IncomeRows.Count / (double)rowsPerPage));
        for (var index = 0; index < pageCount; index++)
        {
            var rows = report.IncomeRows.Skip(index * rowsPerPage).Take(rowsPerPage).ToArray();
            var (graphics, page, y, rowFont, boldFont) = BeginPage(document, report.Year, "Conto economico", generatedAt);
            using (graphics)
            {
                DrawHeader(graphics, y, [("Conto", 50d), ("Descrizione", 230d), ("Costi", 85d), ("Ricavi", 85d), ("Quota", 70d)], boldFont);
                y += 22;
                foreach (var row in rows)
                {
                    DrawCells(graphics, y,
                        [(row.AccountCode.ToString("000"), 50d, false), (row.Description, 230d, false),
                         (Money(row.Costs), 85d, true), (Money(row.Revenues), 85d, true), (Share(row.Share), 70d, true)], rowFont);
                    y += RowHeight;
                }
                if (index == pageCount - 1)
                {
                    DrawTotalLine(graphics, y, [("", 50d, false), ("TOTALI", 230d, false),
                        (Money(report.TotalCosts), 85d, true), (Money(report.TotalRevenues), 85d, true), ("", 70d, true)], boldFont);
                    y += RowHeight;
                    var result = report.ProfitOrLoss;
                    DrawCells(graphics, y, [("", 50d, false),
                        (result < 0 ? "PERDITA DELL'ESERCIZIO" : "UTILE DELL'ESERCIZIO", 230d, false),
                        (result >= 0 ? Money(Math.Abs(result)) : "", 85d, true),
                        (result < 0 ? Money(Math.Abs(result)) : "", 85d, true), ("", 70d, true)], boldFont);
                    graphics.DrawLine(XPens.Gray, Margin, y + RowHeight + 2, Margin + 520, y + RowHeight + 2);
                    graphics.DrawLine(XPens.Gray, Margin, y + RowHeight + 5, Margin + 520, y + RowHeight + 5);
                }
                DrawPageNumber(graphics, page, index + 1, pageCount);
            }
        }
    }

    private static (XGraphics Graphics, PdfPage Page, double Y, XFont RowFont, XFont BoldFont) BeginPage(
        PdfDocument document, int year, string section, DateTime generatedAt)
    {
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        page.Orientation = PdfSharp.PageOrientation.Portrait;
        var graphics = XGraphics.FromPdfPage(page);
        var titleFont = new XFont("Arial", 10, XFontStyleEx.Bold);
        var sectionFont = new XFont("Arial", 9, XFontStyleEx.Regular);
        var rowFont = new XFont("Arial", 8, XFontStyleEx.Regular);
        var boldFont = new XFont("Arial", 8, XFontStyleEx.Bold);
        graphics.DrawString($"BILANCIO DI VERIFICA  -  Esercizio contabile:  {year}", titleFont,
            XBrushes.Black, new XRect(Margin + 70, Margin, 340, 18), XStringFormats.TopLeft);
        graphics.DrawString($"data di stampa:  {generatedAt:dd-MM-yyyy}", rowFont,
            XBrushes.Black, new XRect(page.Width.Point - Margin - 150, Margin, 150, 18), XStringFormats.TopRight);
        graphics.DrawString(section, sectionFont, XBrushes.Black,
            new XRect(Margin + 70, Margin + 28, 300, 18), XStringFormats.TopLeft);
        return (graphics, page, Margin + 62, rowFont, boldFont);
    }

    private static void DrawHeader(XGraphics graphics, double y, (string Text, double Width)[] cells, XFont font)
    {
        var x = Margin;
        foreach (var cell in cells)
        {
            graphics.DrawRectangle(new XSolidBrush(XColor.FromArgb(235, 235, 235)), new XRect(x, y, cell.Width, 18));
            graphics.DrawString(cell.Text, font, XBrushes.Black, new XRect(x + 2, y, cell.Width - 4, 18),
                cell.Text is "Attività" or "Passività" or "Costi" or "Ricavi" or "Quota" ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
            x += cell.Width;
        }
        graphics.DrawLine(XPens.Gray, Margin, y + 18, Margin + cells.Sum(cell => cell.Width), y + 18);
    }

    private static void DrawTotalLine(XGraphics graphics, double y, (string Text, double Width, bool Right)[] cells, XFont font)
    {
        graphics.DrawLine(XPens.Gray, Margin, y - 3, Margin + cells.Sum(cell => cell.Width), y - 3);
        DrawCells(graphics, y, cells, font);
    }

    private static void DrawCells(XGraphics graphics, double y, (string Text, double Width, bool Right)[] cells, XFont font)
    {
        var x = Margin;
        foreach (var cell in cells)
        {
            graphics.DrawString(cell.Text, font, XBrushes.Black, new XRect(x + 2, y, cell.Width - 4, RowHeight),
                cell.Right ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
            x += cell.Width;
        }
    }

    private static void DrawPageNumber(XGraphics graphics, PdfPage page, int number, int count)
    {
        var font = new XFont("Arial", 7, XFontStyleEx.Regular);
        graphics.DrawString($"Pagina {number} di {count}", font, XBrushes.DimGray,
            new XRect(Margin, page.Height.Point - Margin, page.Width.Point - (Margin * 2), 12), XStringFormats.TopRight);
    }

    private static string Money(decimal value) => value == 0 ? "" : value.ToString("N2", Italian);
    private static string Share(decimal value) => value == 0 ? "" : value.ToString("N1", Italian) + " %";
}
