namespace DurableStack.App.Models.Layout;

public sealed class AppBreadcrumbPart
{
    public string Title { get; init; } = string.Empty;

    public string? Url { get; init; }
}
