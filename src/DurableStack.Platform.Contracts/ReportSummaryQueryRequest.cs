using System;
using System.Collections.Generic;

namespace DurableStack.Platform.Contracts;

public sealed class ReportSummaryQueryRequest
{
    public List<Guid> OrganizationIds { get; set; } = new();

    public List<Guid> ProjectIds { get; set; } = new();

    public List<Guid> TenantIds { get; set; } = new();

    public DateTimeOffset? FromUtc { get; set; }

    public DateTimeOffset? ToUtc { get; set; }

    public string? SinceCursor { get; set; }
}
