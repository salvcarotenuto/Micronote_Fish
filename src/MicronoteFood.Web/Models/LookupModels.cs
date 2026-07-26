namespace MicronoteFood.Web.Models;

public sealed record LookupRow(
    int Code,
    string CodeLabel,
    string Label,
    string Detail,
    int? AccountCode = null,
    int? StoreCode = null);
