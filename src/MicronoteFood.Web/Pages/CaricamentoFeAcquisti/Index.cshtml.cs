using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MicronoteFood.Web.Services;

namespace MicronoteFood.Web.Pages.CaricamentoFeAcquisti;

[RequestSizeLimit(31L * 1024L * 1024L)]
public class IndexModel(MicronoteServicePaths servicePaths) : PageModel
{
    private const long MaxFileSize = 30L * 1024L * 1024L;

    public string TransitFolder { get; private set; } = "";

    public void OnGet()
    {
        servicePaths.EnsureCreated();
        TransitFolder = servicePaths.FEAcquistiTransito;
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        servicePaths.EnsureCreated();

        if (file is null || file.Length == 0)
        {
            return new JsonResult(UploadResult.Skipped("", "File vuoto o non valido."));
        }

        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new JsonResult(UploadResult.Skipped(file.FileName, "Nome file non valido."));
        }

        var extension = Path.GetExtension(fileName);
        if (!extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".p7m", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonResult(UploadResult.Skipped(fileName, "Estensione non ammessa."));
        }

        if (file.Length > MaxFileSize)
        {
            return new JsonResult(UploadResult.Error(fileName, "File troppo grande."));
        }

        var destinationPath = Path.Combine(servicePaths.FEAcquistiTransito, fileName);
        if (System.IO.File.Exists(destinationPath))
        {
            return new JsonResult(UploadResult.Existing(fileName, FormatFileSize(file.Length)));
        }

        try
        {
            await using var stream = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                useAsync: true);

            await file.CopyToAsync(stream, cancellationToken);
            return new JsonResult(UploadResult.Uploaded(fileName, FormatFileSize(file.Length)));
        }
        catch (IOException)
        {
            return new JsonResult(UploadResult.Error(fileName, "File non salvato."));
        }
        catch (UnauthorizedAccessException)
        {
            return new JsonResult(UploadResult.Error(fileName, "Permessi insufficienti."));
        }
    }

    private static string FormatFileSize(long size)
    {
        if (size < 1024)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{size} B");
        }

        if (size < 1024 * 1024)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{size / 1024m:N0} KB");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{size / 1024m / 1024m:N1} MB");
    }

    private sealed record UploadResult(
        string Status,
        string FileName,
        string Type,
        string Size,
        string Message)
    {
        public static UploadResult Uploaded(string fileName, string size) =>
            new("uploaded", fileName, FileType(fileName), size, "Caricato.");

        public static UploadResult Existing(string fileName, string size) =>
            new("existing", fileName, FileType(fileName), size, "Gia presente, non sovrascritto.");

        public static UploadResult Skipped(string fileName, string message) =>
            new("skipped", fileName, FileType(fileName), "", message);

        public static UploadResult Error(string fileName, string message) =>
            new("error", fileName, FileType(fileName), "", message);

        private static string FileType(string fileName) =>
            Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant();
    }
}
