using System;
using System.Collections.Generic;

namespace DurableStack.ControlPlane.Entities;

public sealed class Organization
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Guid? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }

    public ICollection<Project> Projects { get; set; } = new List<Project>();

    public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();
}
