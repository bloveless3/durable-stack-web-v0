namespace DurableStack.App.Menu;

public sealed class AppMenuItem
{
    public string Key { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Url { get; init; } = "#";

    public string? IconClass { get; init; }

    public IReadOnlyList<string> AllowedRoles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<AppMenuItem> Children { get; init; } = Array.Empty<AppMenuItem>();
}
