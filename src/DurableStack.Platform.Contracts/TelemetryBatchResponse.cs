namespace DurableStack.Platform.Contracts;

public sealed class TelemetryBatchResponse
{
    public int AcceptedCount { get; set; }

    public int RejectedCount { get; set; }

    public DateTimeOffset ServerTimeUtc { get; set; } = DateTimeOffset.UtcNow;

    public string? IdempotencyKey { get; set; }
}
