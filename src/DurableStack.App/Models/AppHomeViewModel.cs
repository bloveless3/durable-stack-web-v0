namespace DurableStack.App.Models;

public sealed class AppHomeViewModel
{
    public string ApiStatus { get; set; } = "Unknown";

    public string? TenantId { get; set; }

    public int? TotalEvents { get; set; }

    public int? FailedEvents { get; set; }

    public DateTimeOffset? LastEventAtUtc { get; set; }
}
