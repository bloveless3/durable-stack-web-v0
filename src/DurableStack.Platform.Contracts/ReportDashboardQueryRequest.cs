using System;
using System.Collections.Generic;

namespace DurableStack.Platform.Contracts;

public sealed class ReportDashboardQueryRequest
{
    public List<Guid> OrganizationIds { get; set; } = new();

    public List<Guid> ProjectIds { get; set; } = new();

    public List<Guid> TenantIds { get; set; } = new();

    public string Timeframe { get; set; } = "last_24h";
}
