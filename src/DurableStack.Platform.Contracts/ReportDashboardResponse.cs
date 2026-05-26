namespace DurableStack.Platform.Contracts;

public sealed class ReportDashboardResponse
{
    public string[] ScopeTenantIds { get; set; } = [];

    public string Timeframe { get; set; } = "last_24h";

    public DateTimeOffset WindowStartUtc { get; set; }

    public DateTimeOffset WindowEndUtc { get; set; }

    public string BucketSize { get; set; } = "15m";

    public DateTimeOffset QueryRunAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ReportDashboardSummary Summary { get; set; } = new();

    public List<ReportDashboardSeriesPoint> Series { get; set; } = [];

    public ReportDashboardWorkers Workers { get; set; } = new();

    public List<ReportDashboardFailureItem> RecentFailures { get; set; } = [];
}

public sealed class ReportDashboardSummary
{
    public int RunStarted { get; set; }

    public int RunSucceeded { get; set; }

    public int RunFailed { get; set; }

    public int RunRetried { get; set; }

    public int RunsTotal { get; set; }

    public double SuccessRate { get; set; }

    public double FailureRate { get; set; }

    public double RetryRate { get; set; }

    public int HeartbeatCount { get; set; }

    public int ActiveWorkers { get; set; }

    public double? P95DurationMs { get; set; }

    public DateTimeOffset? LastEventAtUtc { get; set; }
}

public sealed class ReportDashboardSeriesPoint
{
    public DateTimeOffset BucketStartUtc { get; set; }

    public int RunStarted { get; set; }

    public int RunSucceeded { get; set; }

    public int RunFailed { get; set; }

    public int RunRetried { get; set; }

    public int HeartbeatCount { get; set; }
}

public sealed class ReportDashboardWorkers
{
    public ReportDashboardWorkerStatusCounts StatusCounts { get; set; } = new();

    public List<ReportDashboardWorkerItem> Items { get; set; } = [];
}

public sealed class ReportDashboardWorkerStatusCounts
{
    public int Online { get; set; }

    public int Warn { get; set; }

    public int Offline { get; set; }
}

public sealed class ReportDashboardWorkerItem
{
    public string WorkerName { get; set; } = string.Empty;

    public string? TenantDisplayName { get; set; }

    public string Status { get; set; } = "offline";

    public DateTimeOffset LastSeenAtUtc { get; set; }

    public int FreshnessSeconds { get; set; }

    public double HeartbeatsPerMinute { get; set; }

    public string? LastJobName { get; set; }

    public string? LastJobOutcome { get; set; }

    public double SuccessRate { get; set; }

    public int RunStarted { get; set; }

    public int RunSucceeded { get; set; }

    public int RunFailed { get; set; }

    public int RunRetried { get; set; }

    public double? P95DurationMs { get; set; }
}

public sealed class ReportDashboardFailureItem
{
    public DateTimeOffset OccurredAtUtc { get; set; }

    public string? JobName { get; set; }

    public string? WorkerName { get; set; }

    public Guid? RunId { get; set; }

    public int? Attempt { get; set; }

    public string? ErrorType { get; set; }

    public string? ErrorMessage { get; set; }

    public double? DurationMs { get; set; }
}
