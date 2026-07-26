namespace MicronoteFood.Web.Models;

public sealed record InitialCustomerSupplierBalanceRow(
    string Type,
    int Code,
    string Name,
    string City,
    string VatNumber,
    decimal Balance);

public sealed record InitialCustomerSupplierBalanceSaveRow(
    string Type,
    int Code,
    decimal Balance);

public sealed record InitialCustomerSupplierBalanceList(
    int Year,
    IReadOnlyList<int> Years,
    IReadOnlyList<InitialCustomerSupplierBalanceRow> Customers,
    IReadOnlyList<InitialCustomerSupplierBalanceRow> Suppliers)
{
    public decimal CustomerTotal => Customers.Sum(row => row.Balance);

    public decimal SupplierTotal => Suppliers.Sum(row => row.Balance);
}
