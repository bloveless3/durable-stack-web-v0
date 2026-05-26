namespace DurableStack.Telemetry.Entities;

public sealed class TelemetryRollupWatermark
{
    public Guid Id { get; set; }

    public string TenantPublicId { get; set; } = string.Empty;

    public string BucketSize { get; set; } = string.Empty;

    public DateTimeOffset LastRolledUpBucketStartUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
