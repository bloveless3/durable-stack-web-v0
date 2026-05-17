using System;

namespace DurableStack.ControlPlane.Entities;

public sealed class OrganizationMember
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }

    public string Role { get; set; } = "Developer";

    public DateTimeOffset JoinedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Organization? Organization { get; set; }

    public User? User { get; set; }
}
