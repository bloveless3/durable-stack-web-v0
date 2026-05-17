namespace DurableStack.Platform.Contracts;

public sealed class ReportSummaryResponse
{
    public string TenantId { get; set; } = string.Empty;

    public int TotalEvents { get; set; }

    public int FailedEvents { get; set; }

    public DateTimeOffset? LastEventAtUtc { get; set; }
}
