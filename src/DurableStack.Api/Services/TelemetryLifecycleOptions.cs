namespace DurableStack.Api.Services;

public sealed class TelemetryLifecycleOptions
{
    public const string SectionName = "TelemetryLifecycle";
    public const string ExecutionModeLocal = "local";
    public const string ExecutionModeDurableStack = "durablestack";

    public string ExecutionMode { get; set; } = ExecutionModeLocal;

    public TelemetryRollupWorkerOptions RollupWorker { get; set; } = new();

    public TelemetryRetentionWorkerOptions RetentionWorker { get; set; } = new();

    public TelemetryQueryOptions Query { get; set; } = new();

    public static string NormalizeExecutionMode(string? executionMode)
    {
        if (string.IsNullOrWhiteSpace(executionMode))
        {
            return ExecutionModeLocal;
        }

        var normalized = executionMode.Trim().ToLowerInvariant();
        return normalized switch
        {
            ExecutionModeLocal => ExecutionModeLocal,
            ExecutionModeDurableStack => ExecutionModeDurableStack,
            _ => ExecutionModeLocal
        };
    }
}

public sealed class TelemetryRollupWorkerOptions
{
    public bool Enabled { get; set; }

    public int IntervalMinutes { get; set; } = 5;

    public int FinalizationLagMinutes { get; set; } = 60;

    public int MaxBucketsPerTenantPerRun { get; set; } = 96;

    public List<string> BucketSizes { get; set; } = ["15m", "2h", "12h"];
}

public sealed class TelemetryRetentionWorkerOptions
{
    public bool Enabled { get; set; }

    public bool DryRun { get; set; } = true;

    public int IntervalHours { get; set; } = 24;

    public int FreeRetentionDays { get; set; } = 7;

    public int PaidRetentionDays { get; set; } = 730;

    public List<string> PaidTenantPublicIds { get; set; } = [];
}

public sealed class TelemetryQueryOptions
{
    public bool EnableHybridRollupReads { get; set; }
}
