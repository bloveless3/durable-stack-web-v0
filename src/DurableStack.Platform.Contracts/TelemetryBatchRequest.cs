using System;
using System.Collections.Generic;

namespace DurableStack.Platform.Contracts;

public sealed class TelemetryBatchRequest
{
    public string TenantId { get; set; } = string.Empty;

    public string? IdempotencyKey { get; set; }

    public string? ServiceName { get; set; }

    public string? EnvironmentName { get; set; }

    public List<TelemetryEventDto> Events { get; set; } = new();
}

public sealed class TelemetryEventDto
{
    public string EventType { get; set; } = string.Empty;

    public int EventVersion { get; set; } = 1;

    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Guid? RunId { get; set; }

    public string? JobName { get; set; }

    public int? Attempt { get; set; }

    public string? WorkerName { get; set; }

    public double? DurationMs { get; set; }

    public string? ErrorType { get; set; }

    public string? ErrorMessage { get; set; }

    public string? PayloadJson { get; set; }
}
