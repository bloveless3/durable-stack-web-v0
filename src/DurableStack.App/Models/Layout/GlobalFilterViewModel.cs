namespace DurableStack.App.Models.Layout;

public sealed class GlobalFilterViewModel
{
    public string Organization { get; init; } = "all-organizations";

    public string Project { get; init; } = "all-projects";

    public string Environment { get; init; } = "all-environments";

    public string TimeRange { get; init; } = "24h";
}
