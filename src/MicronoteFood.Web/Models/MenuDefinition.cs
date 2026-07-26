namespace MicronoteFood.Web.Models;

public sealed record MenuItemDefinition(string Label, string? Page = null, string? ReturnTo = null);

public sealed record MenuGroupDefinition(
    string Title,
    IReadOnlyList<MenuItemDefinition> Items);

public sealed record MenuSectionDefinition(
    string Id,
    string Title,
    string Icon,
    string AccentClass,
    IReadOnlyList<MenuGroupDefinition> Groups);

public sealed record QuickLinkDefinition(
    string Label,
    string Icon,
    string? Page = null);
