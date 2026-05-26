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

public sealed class ReportDashboardFailureDetailsQueryRequest
{
    public List<Guid> OrganizationIds { get; set; } = new();

    public List<Guid> ProjectIds { get; set; } = new();

    public List<Guid> TenantIds { get; set; } = new();

    public string Timeframe { get; set; } = "last_24h";

    public string JobName { get; set; } = string.Empty;

    public string ErrorType { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;
}

public sealed class ReportDashboardFailureDetailsResponse
{
    public string Timeframe { get; set; } = "last_24h";

    public string JobName { get; set; } = string.Empty;

    public string ErrorType { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;

    public int SampleCount { get; set; }

    public List<ReportDashboardFailureSampleItem> Samples { get; set; } = [];
}

public sealed class ReportDashboardFailureSampleItem
{
    public string TenantDisplayName { get; set; } = "N/A";

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string WorkerName { get; set; } = "(unknown)";

    public int? Attempt { get; set; }

    public Guid? RunId { get; set; }

    public string? ErrorDetail { get; set; }

    public string? PayloadJson { get; set; }
}
