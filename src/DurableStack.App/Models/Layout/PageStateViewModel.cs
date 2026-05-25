namespace DurableStack.App.Models.Layout;

public sealed class PageStateViewModel
{
    public string Type { get; init; } = "info";

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? ActionText { get; init; }

    public string? ActionUrl { get; init; }
}
