using System;

namespace DurableStack.ControlPlane.Entities;

public sealed class TenantCredential
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string ClientSecretHash { get; set; } = string.Empty;

    public string CredentialName { get; set; } = "default";

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public Tenant? Tenant { get; set; }
}
