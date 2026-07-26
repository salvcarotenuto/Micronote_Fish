using System.Globalization;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed partial class MicronoteDatabaseOptions
{
    public const int DefaultCompanyCode = 1;
    public const string DefaultMasterDatabase = "Micronote_Master";
    public const string DefaultCompanyDatabasePattern = "Micronote_{0:0000}";

    private readonly string _baseConnectionString;

    public MicronoteDatabaseOptions(IConfiguration configuration)
    {
        _baseConnectionString = configuration.GetConnectionString("MicronoteServer")
            ?? configuration.GetConnectionString("MicronoteDb")
            ?? throw new InvalidOperationException(
                "Stringa di connessione 'MicronoteServer' o 'MicronoteDb' non configurata.");

        var masterDatabase = configuration["Micronote:Database:MasterDatabase"]
            ?? DefaultMasterDatabase;
        var companyPattern = configuration["Micronote:Database:CompanyDatabasePattern"]
            ?? DefaultCompanyDatabasePattern;

        UseCompanyDatabase = configuration.GetValue("Micronote:MultiCompany:Enabled", false);
        MasterDatabase = ValidateDatabaseName(masterDatabase);
        CompanyDatabasePattern = companyPattern;
    }

    public bool UseCompanyDatabase { get; }

    public string MasterDatabase { get; }

    public string CompanyDatabasePattern { get; }

    public string BuildDefaultConnectionString() => _baseConnectionString;

    public string BuildMasterConnectionString() => BuildConnectionString(MasterDatabase);

    public string BuildCompanyConnectionString(int companyCode) =>
        BuildConnectionString(BuildCompanyDatabaseName(companyCode));

    public string BuildCompanyConnectionString(string databaseName) =>
        BuildConnectionString(databaseName);

    public string BuildCompanyDatabaseName(int companyCode)
    {
        if (companyCode is < 1 or > 9999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(companyCode),
                "Il codice azienda deve essere compreso tra 1 e 9999.");
        }

        return ValidateDatabaseName(string.Format(
            CultureInfo.InvariantCulture,
            CompanyDatabasePattern,
            companyCode));
    }

    public static string CompanyKey(int companyCode) =>
        companyCode.ToString("0000", CultureInfo.InvariantCulture);

    private string BuildConnectionString(string databaseName)
    {
        var builder = new MySqlConnectionStringBuilder(_baseConnectionString)
        {
            Database = ValidateDatabaseName(databaseName)
        };

        return builder.ConnectionString;
    }

    private static string ValidateDatabaseName(string databaseName)
    {
        var value = databaseName.Trim();
        if (!DatabaseNameRegex().IsMatch(value))
        {
            throw new InvalidOperationException(
                $"Nome database non valido: '{databaseName}'.");
        }

        return value;
    }

    [GeneratedRegex("^[A-Za-z0-9_]+$")]
    private static partial Regex DatabaseNameRegex();
}


