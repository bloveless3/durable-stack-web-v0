namespace DurableStack.App.Models.Reports;

public sealed class DashboardDataResponse
{
    public string Status { get; set; } = "Unavailable";

    public string Timeframe { get; set; } = "last_24h";

    public string BucketSize { get; set; } = "15m";

    public string LastEventAtUtc { get; set; } = "N/A";

    public string QueryRunAtUtc { get; set; } = "N/A";

    public DashboardSummaryData Summary { get; set; } = new();

    public List<DashboardSeriesPointData> Series { get; set; } = [];

    public DashboardWorkersData Workers { get; set; } = new();

    public List<DashboardFailureData> RecentFailures { get; set; } = [];
}

public sealed class DashboardSummaryData
{
    public int RunStarted { get; set; }

    public int RunSucceeded { get; set; }

    public int RunFailed { get; set; }

    public int RunRetried { get; set; }

    public int RunsTotal { get; set; }

    public string SuccessRate { get; set; } = "0.0%";

    public string FailureRate { get; set; } = "0.0%";

    public string RetryRate { get; set; } = "0.0%";

    public int HeartbeatCount { get; set; }

    public int ActiveWorkers { get; set; }

    public string P95DurationMs { get; set; } = "N/A";
}

public sealed class DashboardSeriesPointData
{
    public string BucketStartUtc { get; set; } = string.Empty;

    public int RunStarted { get; set; }

    public int RunSucceeded { get; set; }

    public int RunFailed { get; set; }

    public int RunRetried { get; set; }

    public int HeartbeatCount { get; set; }
}

public sealed class DashboardWorkersData
{
    public DashboardWorkerStatusData StatusCounts { get; set; } = new();

    public List<DashboardWorkerItemData> Items { get; set; } = [];
}

public sealed class DashboardWorkerStatusData
{
    public int Online { get; set; }

    public int Warn { get; set; }

    public int Offline { get; set; }
}

public sealed class DashboardWorkerItemData
{
    public string WorkerName { get; set; } = string.Empty;

    public string TenantDisplayName { get; set; } = "N/A";

    public string Status { get; set; } = "offline";

    public string LastSeenAtUtc { get; set; } = "N/A";

    public int FreshnessSeconds { get; set; }

    public string HeartbeatsPerMinute { get; set; } = "0.0";

    public string? LastJobName { get; set; }

    public string? LastJobOutcome { get; set; }

    public string SuccessRate { get; set; } = "0.0%";

    public int RunStarted { get; set; }

    public int RunSucceeded { get; set; }

    public int RunFailed { get; set; }

    public int RunRetried { get; set; }

    public string P95DurationMs { get; set; } = "N/A";
}

public sealed class DashboardFailureData
{
    public string OccurredAtUtc { get; set; } = string.Empty;

    public string JobName { get; set; } = "(unknown)";

    public string WorkerName { get; set; } = "(unknown)";

    public string RunId { get; set; } = "N/A";

    public string Attempt { get; set; } = "N/A";

    public string ErrorType { get; set; } = "N/A";

    public string ErrorMessage { get; set; } = "N/A";

    public string DurationMs { get; set; } = "N/A";
}
