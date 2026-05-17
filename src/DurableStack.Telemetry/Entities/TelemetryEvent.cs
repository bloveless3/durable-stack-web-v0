using System;

namespace DurableStack.Telemetry.Entities;

public sealed class TelemetryEvent
{
    public Guid Id { get; set; }

    public Guid BatchId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public int EventVersion { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string? JobName { get; set; }

    public Guid? RunId { get; set; }

    public int? Attempt { get; set; }

    public string? WorkerName { get; set; }

    public double? DurationMs { get; set; }

    public string? ErrorType { get; set; }

    public string? ErrorMessage { get; set; }

    public string? PayloadJson { get; set; }

    public TelemetryBatch? Batch { get; set; }
}
