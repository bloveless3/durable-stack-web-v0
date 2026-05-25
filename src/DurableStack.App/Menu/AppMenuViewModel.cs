namespace DurableStack.App.Menu;

public sealed class AppMenuViewModel
{
    public IReadOnlyList<AppMenuItem> Items { get; init; } = Array.Empty<AppMenuItem>();

    public string? ActiveKey { get; init; }
}
