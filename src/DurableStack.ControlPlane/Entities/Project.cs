using System;
using System.Collections.Generic;

namespace DurableStack.ControlPlane.Entities;

public sealed class Project
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Organization? Organization { get; set; }

    public ICollection<Tenant> Tenants { get; set; } = new List<Tenant>();
}
