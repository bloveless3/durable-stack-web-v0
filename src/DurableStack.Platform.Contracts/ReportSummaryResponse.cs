namespace DurableStack.Platform.Contracts;

public sealed class ReportSummaryResponse
{
    public string? TenantId { get; set; }

    public int TotalEvents { get; set; }

    public int FailedEvents { get; set; }

    public DateTimeOffset? LastEventAtUtc { get; set; }

    public string[] ScopeTenantIds { get; set; } = [];

    public DateTimeOffset? WindowStartUtc { get; set; }

    public DateTimeOffset? WindowEndUtc { get; set; }

    public DateTimeOffset QueryRunAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string? NextCursor { get; set; }
}
