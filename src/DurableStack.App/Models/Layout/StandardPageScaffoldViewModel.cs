namespace DurableStack.App.Models.Layout;

public sealed class StandardPageScaffoldViewModel
{
    public string Eyebrow { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<string> ChecklistItems { get; init; } = Array.Empty<string>();

    public PageStateViewModel? PrimaryState { get; init; }

    public PageStateViewModel? SecondaryState { get; init; }
}
