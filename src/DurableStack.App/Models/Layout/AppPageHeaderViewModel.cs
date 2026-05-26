using Microsoft.AspNetCore.Html;

namespace DurableStack.App.Models.Layout;

public sealed class AppPageHeaderViewModel
{
    public string Title { get; init; } = "DurableStack";

    public IReadOnlyList<AppBreadcrumbPart> Breadcrumbs { get; init; } = Array.Empty<AppBreadcrumbPart>();

    public bool ShowGlobalFilters { get; init; } = true;

    public GlobalFilterViewModel GlobalFilters { get; init; } = new();

    public IReadOnlyList<GlobalFilterOption> OrganizationOptions { get; init; } = [];

    public IReadOnlyList<GlobalFilterOption> ProjectOptions { get; init; } = [];

    public IReadOnlyList<GlobalFilterOption> TenantOptions { get; init; } = [];

    public IHtmlContent? PageControls { get; init; }
}
