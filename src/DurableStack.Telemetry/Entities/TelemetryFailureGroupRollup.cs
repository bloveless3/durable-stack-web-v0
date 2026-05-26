namespace DurableStack.Telemetry.Entities;

public sealed class TelemetryFailureGroupRollup
{
    public Guid Id { get; set; }

    public string TenantPublicId { get; set; } = string.Empty;

    public string BucketSize { get; set; } = string.Empty;

    public DateTimeOffset BucketStartUtc { get; set; }

    public string JobName { get; set; } = "(unknown)";

    public string ErrorType { get; set; } = "(unknown)";

    public string ErrorMessage { get; set; } = "(unknown)";

    public int FailureCount { get; set; }

    public DateTimeOffset FirstOccurredAtUtc { get; set; }

    public DateTimeOffset LastOccurredAtUtc { get; set; }

    public DateTimeOffset ComputedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
