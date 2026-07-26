using MicronoteFood.Web.Models;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace MicronoteFood.Web.Services;

public sealed class SupplierListPdfService
{
    static SupplierListPdfService()
    {
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    private static readonly (string Title, double Width, Func<SupplierListItem, string> Value)[] Columns =
    [
        ("Codice", 42, row => row.Code.ToString("00000")),
        ("Nome", 145, row => row.Name),
        ("Sede", 78, row => row.City),
        ("Prov.", 32, row => row.Province),
        ("Partita IVA", 72, row => row.VatNumber),
        ("Telefono", 72, row => row.Phone),
        ("E-mail", 118, row => row.Email),
        ("Contropartita", 118, row => row.AccountDescription),
        ("P. vendita", 76, row => row.StoreName)
    ];

    private const double Margin = 28;
    private const double HeaderHeight = 20;
    private const double RowHeight = 17;

    public byte[] Create(IReadOnlyList<SupplierListItem> suppliers, DateTime generatedAt)
    {
        using var document = new PdfDocument();
        document.Info.Title = "Lista fornitori";
        document.Info.Subject = "Elenco fornitori filtrato e ordinato dall'utente";
        document.Info.Creator = "Micronote Food";

        var titleFont = new XFont("Arial", 16, XFontStyleEx.Bold);
        var subtitleFont = new XFont("Arial", 8, XFontStyleEx.Regular);
        var headerFont = new XFont("Arial", 8, XFontStyleEx.Bold);
        var rowFont = new XFont("Arial", 7.5, XFontStyleEx.Regular);
        var inactiveFont = new XFont("Arial", 7.5, XFontStyleEx.Italic);
        var footerFont = new XFont("Arial", 7, XFontStyleEx.Regular);

        const int rowsPerPage = 34;
        var pageCount = Math.Max(1, (int)Math.Ceiling(suppliers.Count / (double)rowsPerPage));

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            page.Orientation = PdfSharp.PageOrientation.Landscape;

            using var graphics = XGraphics.FromPdfPage(page);
            var width = page.Width.Point;
            var height = page.Height.Point;

            graphics.DrawString(
                "Lista fornitori",
                titleFont,
                XBrushes.Black,
                new XRect(Margin, Margin, width - (Margin * 2), 24),
                XStringFormats.TopLeft);
            graphics.DrawString(
                $"Elaborazione del {generatedAt:dd/MM/yyyy HH:mm}  •  {suppliers.Count} record",
                subtitleFont,
                XBrushes.DimGray,
                new XRect(Margin, Margin + 25, width - (Margin * 2), 15),
                XStringFormats.TopLeft);

            var top = Margin + 48;
            DrawHeader(graphics, top, headerFont);

            var pageRows = suppliers
                .Skip(pageIndex * rowsPerPage)
                .Take(rowsPerPage)
                .ToArray();

            for (var rowIndex = 0; rowIndex < pageRows.Length; rowIndex++)
            {
                var supplier = pageRows[rowIndex];
                var y = top + HeaderHeight + (rowIndex * RowHeight);
                DrawRow(
                    graphics,
                    supplier,
                    y,
                    rowIndex % 2 == 1,
                    supplier.IsActive ? rowFont : inactiveFont);
            }

            graphics.DrawString(
                $"Pagina {pageIndex + 1} di {pageCount}",
                footerFont,
                XBrushes.DimGray,
                new XRect(Margin, height - Margin + 4, width - (Margin * 2), 12),
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

    private static void DrawRow(
        XGraphics graphics,
        SupplierListItem supplier,
        double y,
        bool alternate,
        XFont font)
    {
        var x = Margin;
        var background = !supplier.IsActive
            ? XColor.FromArgb(245, 231, 231)
            : alternate
                ? XColor.FromArgb(239, 244, 250)
                : XColors.White;

        foreach (var column in Columns)
        {
            var rectangle = new XRect(x, y, column.Width, RowHeight);
            graphics.DrawRectangle(new XSolidBrush(background), rectangle);
            graphics.DrawRectangle(new XPen(XColor.FromArgb(188, 202, 218), 0.45), rectangle);
            DrawCellText(graphics, column.Value(supplier), font, XBrushes.Black, rectangle, 3);
            x += column.Width;
        }
    }

    private static void DrawCellText(
        XGraphics graphics,
        string? value,
        XFont font,
        XBrush brush,
        XRect rectangle,
        double padding)
    {
        var text = value?.Trim() ?? "";
        var maximumWidth = Math.Max(0, rectangle.Width - (padding * 2));

        if (graphics.MeasureString(text, font).Width > maximumWidth)
        {
            const string ellipsis = "…";
            while (text.Length > 0
                   && graphics.MeasureString(text + ellipsis, font).Width > maximumWidth)
            {
                text = text[..^1];
            }
            text += ellipsis;
        }

        graphics.DrawString(
            text,
            font,
            brush,
            new XRect(
                rectangle.X + padding,
                rectangle.Y,
                maximumWidth,
                rectangle.Height),
            XStringFormats.CenterLeft);
    }
}
