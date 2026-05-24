using Microsoft.AspNetCore.Html;

namespace DurableStack.App.Models.Layout;

public sealed class AppPageHeaderViewModel
{
    public string Title { get; init; } = "DurableStack";

    public IReadOnlyList<string> Breadcrumbs { get; init; } = Array.Empty<string>();

    public IHtmlContent? PageControls { get; init; }
}
