namespace DurableStack.App.Models.Reports;

public sealed class DashboardSummaryResponse
{
    public string Status { get; set; } = "Unavailable";

    public int TotalEvents { get; set; }

    public int FailedEvents { get; set; }

    public string LastEventAtUtc { get; set; } = "N/A";

    public string QueryRunAtUtc { get; set; } = "N/A";

    public string? NextCursor { get; set; }
}
