using System;
using System.Collections.Generic;

namespace DurableStack.ControlPlane.Entities;

public sealed class Tenant
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string EnvironmentName { get; set; } = string.Empty;

    public string PublicTenantId { get; set; } = string.Empty;

    public bool DetailedErrorSyncEnabled { get; set; }

    public bool SyncEnabled { get; set; } = true;

    public int MaxBatchSize { get; set; } = 1000;

    public int RecommendedBatchIntervalSeconds { get; set; } = 15;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Project? Project { get; set; }

    public ICollection<TenantCredential> Credentials { get; set; } = new List<TenantCredential>();
}
