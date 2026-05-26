namespace DurableStack.Telemetry.Entities;

public sealed class TelemetryBucketRollup
{
    public Guid Id { get; set; }

    public string TenantPublicId { get; set; } = string.Empty;

    public string BucketSize { get; set; } = string.Empty;

    public DateTimeOffset BucketStartUtc { get; set; }

    public int RunStarted { get; set; }

    public int RunSucceeded { get; set; }

    public int RunFailed { get; set; }

    public int RunRetried { get; set; }

    public int HeartbeatCount { get; set; }

    public DateTimeOffset? LastEventAtUtc { get; set; }

    public DateTimeOffset ComputedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
