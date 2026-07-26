using MicronoteFood.Web.Services;
using MySqlConnector;

namespace MicronoteFood.Web.Data;

public sealed class MicronoteDb(
    MicronoteDatabaseOptions options,
    CurrentCompanyContext companyContext)
{
    public string CurrentCompanyDatabaseName =>
        ResolveCurrentCompanyDatabaseName();

    public string MasterDatabaseName => options.MasterDatabase;

    public async Task<MySqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        if (options.UseCompanyDatabase)
        {
            return await OpenCompanyConnectionAsync(
                ResolveCurrentCompanyDatabaseName(),
                cancellationToken);
        }

        var connection = new MySqlConnection(options.BuildDefaultConnectionString());
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public async Task<MySqlConnection> OpenCompanyConnectionAsync(
        int companyCode,
        CancellationToken cancellationToken = default)
        => await OpenCompanyConnectionAsync(
            options.BuildCompanyDatabaseName(companyCode),
            cancellationToken);

    public async Task<MySqlConnection> OpenCompanyConnectionAsync(
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(
            options.BuildCompanyConnectionString(databaseName));
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public async Task<MySqlConnection> OpenMasterConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(options.BuildMasterConnectionString());
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private string ResolveCurrentCompanyDatabaseName() =>
        string.IsNullOrWhiteSpace(companyContext.DatabaseName)
            ? options.BuildCompanyDatabaseName(companyContext.Code)
            : companyContext.DatabaseName.Trim();
}
