using MicronoteFood.Web.Data;

namespace MicronoteFood.Web.Services;

public sealed class MicronoteServicePaths
{
    private static readonly string[] ServiceFolderNames =
    [
        "FEAcquisti",
        "FEVendite",
        "Excel",
        "Pdf",
        "Titoli"
    ];

    public MicronoteServicePaths(
        IConfiguration configuration,
        CurrentCompanyContext companyContext)
    {
        CompanyKey = companyContext.CodeText;

        Root = configuration["Micronote:DataRoot"]
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "MicronoteFood");

        CompanyRoot = Path.Combine(Root, "Aziende", CompanyKey);
        FEAcquisti = Path.Combine(CompanyRoot, "FEAcquisti");
        FEAcquistiTransito = Path.Combine(FEAcquisti, "Transito");
        FEAcquistiArchivio = Path.Combine(FEAcquisti, "Archivio");
        FEVendite = Path.Combine(CompanyRoot, "FEVendite");
        Excel = Path.Combine(CompanyRoot, "Excel");
        Pdf = Path.Combine(CompanyRoot, "Pdf");
        Titoli = Path.Combine(CompanyRoot, "Titoli");
    }

    public string CompanyKey { get; }

    public string Root { get; }

    public string CompanyRoot { get; }

    public string FEAcquisti { get; }

    public string FEAcquistiTransito { get; }

    public string FEAcquistiArchivio { get; }

    public string FEVendite { get; }

    public string Excel { get; }

    public string Pdf { get; }

    public string Titoli { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(CompanyRoot);

        foreach (var folderName in ServiceFolderNames)
        {
            Directory.CreateDirectory(Path.Combine(CompanyRoot, folderName));
        }

        Directory.CreateDirectory(FEAcquistiTransito);
        Directory.CreateDirectory(FEAcquistiArchivio);
    }
}
