namespace DurableStack.App.Models.Layout;

public sealed class AppToastNotification
{
    public string Type { get; init; } = "info";

    public string Message { get; init; } = string.Empty;

    public int TimeoutMs { get; init; } = 0;
}
