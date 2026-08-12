using System.Globalization;
using MicronoteFood.Web.Models;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace MicronoteFood.Web.Services;

public sealed class ArticleListPdfService
{
    static ArticleListPdfService()
    {
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");
    private static readonly (string Title, double Width, Func<ArticleListItem, string> Value)[] Columns =
    [
        ("Codice", 70, row => row.Code),
        ("Descrizione", 135, row => row.Description),
        ("U.m.a.", 25, row => row.SalesUnitCode),
        ("U.m.v.", 25, row => row.PurchaseUnitCode),
        ("Categoria", 55, row => row.CategoryDescription),
        ("Gruppo", 55, row => row.GroupDescription),
        ("Specie", 55, row => row.SpeciesDescription),
        ("Provenienza", 60, row => row.OriginDescription),
        ("Costo", 45, row => row.StandardCost == 0 ? "" : row.StandardCost.ToString("N3", Italian)),
        ("Prezzo", 45, row => row.StandardPrice == 0 ? "" : row.StandardPrice.ToString("N3", Italian)),
        ("Ivato", 45, row => row.VatIncludedPrice == 0 ? "" : row.VatIncludedPrice.ToString("N2", Italian)),
        ("IVA", 35, row => row.VatRate == 0 ? "" : row.VatRate.ToString("N0", Italian) + " %"),
        ("Fornitore", 70, row => row.SupplierName)
    ];

    private const double Margin = 28;
    private const double HeaderHeight = 20;
    private const double RowHeight = 15;

    public byte[] Create(IReadOnlyList<ArticleListItem> articles, DateTime generatedAt)
    {
        using var document = new PdfDocument();
        document.Info.Title = "Lista articoli";
        document.Info.Subject = "Elenco articoli filtrato e ordinato dall'utente";
        document.Info.Creator = "Micronote Food";

        var titleFont = new XFont("Arial", 16, XFontStyleEx.Bold);
        var subtitleFont = new XFont("Arial", 8, XFontStyleEx.Regular);
        var headerFont = new XFont("Arial", 7.5, XFontStyleEx.Bold);
        var rowFont = new XFont("Arial", 7, XFontStyleEx.Regular);
        var footerFont = new XFont("Arial", 7, XFontStyleEx.Regular);
        const int rowsPerPage = 38;
        var pageCount = Math.Max(1, (int)Math.Ceiling(articles.Count / (double)rowsPerPage));

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            page.Orientation = PdfSharp.PageOrientation.Landscape;
            using var graphics = XGraphics.FromPdfPage(page);
            var width = page.Width.Point;
            var height = page.Height.Point;

            graphics.DrawString("Lista articoli", titleFont, XBrushes.Black,
                new XRect(Margin, Margin, width - (Margin * 2), 24), XStringFormats.TopLeft);
            graphics.DrawString(
                $"Elaborazione del {generatedAt:dd/MM/yyyy HH:mm}  •  {articles.Count} record",
                subtitleFont, XBrushes.DimGray,
                new XRect(Margin, Margin + 25, width - (Margin * 2), 15), XStringFormats.TopLeft);

            var top = Margin + 48;
            DrawHeader(graphics, top, headerFont);
            var pageRows = articles.Skip(pageIndex * rowsPerPage).Take(rowsPerPage).ToArray();
            for (var rowIndex = 0; rowIndex < pageRows.Length; rowIndex++)
            {
                DrawRow(graphics, pageRows[rowIndex], top + HeaderHeight + (rowIndex * RowHeight),
                    rowIndex % 2 == 1, rowFont);
            }

            graphics.DrawString($"Pagina {pageIndex + 1} di {pageCount}", footerFont,
                XBrushes.DimGray, new XRect(Margin, height - Margin + 4, width - (Margin * 2), 12),
                XStringFormats.TopRight);
        }

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static void DrawHeader(XGraphics graphics, double y, XFont font)
    {
        var x = Margin;
        foreach (var column in Columns)
        {
            var rectangle = new XRect(x, y, column.Width, HeaderHeight);
            graphics.DrawRectangle(new XSolidBrush(XColor.FromArgb(68, 93, 153)), rectangle);
            DrawCellText(graphics, column.Title, font, XBrushes.White, rectangle, 4);
            x += column.Width;
        }
    }

    private static void DrawRow(XGraphics graphics, ArticleListItem article, double y,
        bool alternate, XFont font)
    {
        var x = Margin;
        var background = alternate ? XColor.FromArgb(239, 244, 250) : XColors.White;
        foreach (var column in Columns)
        {
            var rectangle = new XRect(x, y, column.Width, RowHeight);
            graphics.DrawRectangle(new XSolidBrush(background), rectangle);
            graphics.DrawRectangle(new XPen(XColor.FromArgb(188, 202, 218), 0.45), rectangle);
            DrawCellText(graphics, column.Value(article), font, XBrushes.Black, rectangle, 3);
            x += column.Width;
        }
    }

    private static void DrawCellText(XGraphics graphics, string? value, XFont font,
        XBrush brush, XRect rectangle, double padding)
    {
        var text = value?.Trim() ?? "";
        var maximumWidth = Math.Max(0, rectangle.Width - (padding * 2));
        if (graphics.MeasureString(text, font).Width > maximumWidth)
        {
            const string ellipsis = "…";
            while (text.Length > 0 && graphics.MeasureString(text + ellipsis, font).Width > maximumWidth)
            {
                text = text[..^1];
            }
            text += ellipsis;
        }
        graphics.DrawString(text, font, brush,
            new XRect(rectangle.X + padding, rectangle.Y, maximumWidth, rectangle.Height),
            XStringFormats.CenterLeft);
    }
}
