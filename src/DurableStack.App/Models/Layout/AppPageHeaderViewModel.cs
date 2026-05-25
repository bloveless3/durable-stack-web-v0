using Microsoft.AspNetCore.Html;

namespace DurableStack.App.Models.Layout;

public sealed class AppPageHeaderViewModel
{
    public string Title { get; init; } = "DurableStack";

    public IReadOnlyList<AppBreadcrumbPart> Breadcrumbs { get; init; } = Array.Empty<AppBreadcrumbPart>();

    public bool ShowGlobalFilters { get; init; } = true;

    public GlobalFilterViewModel GlobalFilters { get; init; } = new();

    public IHtmlContent? PageControls { get; init; }
}
