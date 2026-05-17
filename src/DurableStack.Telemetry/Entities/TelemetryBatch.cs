using System;
using System.Collections.Generic;

namespace DurableStack.Telemetry.Entities;

public sealed class TelemetryBatch
{
    public Guid Id { get; set; }

    public string TenantPublicId { get; set; } = string.Empty;

    public string? ServiceName { get; set; }

    public string? EnvironmentName { get; set; }

    public string? IdempotencyKey { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public int AcceptedCount { get; set; }

    public int RejectedCount { get; set; }

    public ICollection<TelemetryEvent> Events { get; set; } = new List<TelemetryEvent>();
}
