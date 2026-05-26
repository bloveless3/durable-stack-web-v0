using DurableStack.App.Models.Reports;
using DurableStack.App.Services.Api;
using DurableStack.App.Services.Preferences;
using DurableStack.Platform.Contracts;

namespace DurableStack.App.Services.Reports;

public interface IReportsQueryService
{
    Task<DashboardDataResponse?> QueryDashboardAsync(string? correlationId, CancellationToken cancellationToken = default);

    Task<DashboardFailureDetailsResponse?> QueryFailureDetailsAsync(string? correlationId, string jobName, string errorType, string errorMessage, CancellationToken cancellationToken = default);
}

public sealed class ReportsQueryService : IReportsQueryService
{
    private readonly IUserPreferenceService _userPreferenceService;
    private readonly IReportsApiClient _reportsApiClient;
    private readonly IUserApiTokenService _userApiTokenService;

    public ReportsQueryService(
        IUserPreferenceService userPreferenceService,
        IReportsApiClient reportsApiClient,
        IUserApiTokenService userApiTokenService)
    {
        _userPreferenceService = userPreferenceService;
        _reportsApiClient = reportsApiClient;
        _userApiTokenService = userApiTokenService;
    }

    public async Task<DashboardDataResponse?> QueryDashboardAsync(
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var preferenceValues = await _userPreferenceService.GetValuesAsync(PreferenceKeys.GlobalFilterKeys, cancellationToken);

        var dashboardQuery = new ReportDashboardQueryRequest
        {
            Timeframe = MapTimeRangeToApiTimeframe(preferenceValues),
            TenantIds = ParseGuidList(preferenceValues, PreferenceKeys.GlobalFilterTenant, "all-tenants"),
            ProjectIds = ParseGuidList(preferenceValues, PreferenceKeys.GlobalFilterProject, "all-projects"),
            OrganizationIds = ParseGuidList(preferenceValues, PreferenceKeys.GlobalFilterOrganization, "all-organizations")
        };

        var token = await _userApiTokenService.CreateReportsReadTokenAsync(cancellationToken);
        var dashboard = await _reportsApiClient.GetDashboardAsync(dashboardQuery, token, correlationId, cancellationToken);

        if (dashboard is null)
        {
            return null;
        }

        return new DashboardDataResponse
        {
            Status = "Connected",
            Timeframe = dashboard.Timeframe,
            BucketSize = dashboard.BucketSize,
            LastEventAtUtc = FormatUtcOrFallback(dashboard.Summary.LastEventAtUtc),
            QueryRunAtUtc = FormatUtc(dashboard.QueryRunAtUtc),
            Summary = new DashboardSummaryData
            {
                RunStarted = dashboard.Summary.RunStarted,
                RunSucceeded = dashboard.Summary.RunSucceeded,
                RunFailed = dashboard.Summary.RunFailed,
                RunRetried = dashboard.Summary.RunRetried,
                RunsTotal = dashboard.Summary.RunsTotal,
                SuccessRate = FormatPercent(dashboard.Summary.SuccessRate),
                FailureRate = FormatPercent(dashboard.Summary.FailureRate),
                RetryRate = FormatPercent(dashboard.Summary.RetryRate),
                HeartbeatCount = dashboard.Summary.HeartbeatCount,
                ActiveWorkers = dashboard.Summary.ActiveWorkers,
                P95DurationMs = FormatNumberOrFallback(dashboard.Summary.P95DurationMs)
            },
            Series = dashboard.Series
                .Select(x => new DashboardSeriesPointData
                {
                    BucketStartUtc = FormatUtc(x.BucketStartUtc),
                    RunStarted = x.RunStarted,
                    RunSucceeded = x.RunSucceeded,
                    RunFailed = x.RunFailed,
                    RunRetried = x.RunRetried,
                    HeartbeatCount = x.HeartbeatCount
                })
                .ToList(),
            Workers = new DashboardWorkersData
            {
                StatusCounts = new DashboardWorkerStatusData
                {
                    Online = dashboard.Workers.StatusCounts.Online,
                    Warn = dashboard.Workers.StatusCounts.Warn,
                    Offline = dashboard.Workers.StatusCounts.Offline
                },
                Items = dashboard.Workers.Items
                    .Select(x => new DashboardWorkerItemData
                    {
                        WorkerName = x.WorkerName,
                        TenantDisplayName = string.IsNullOrWhiteSpace(x.TenantDisplayName) ? "N/A" : x.TenantDisplayName,
                        Status = x.Status,
                        LastSeenAtUtc = FormatUtc(x.LastSeenAtUtc),
                        FreshnessSeconds = x.FreshnessSeconds,
                        HeartbeatsPerMinute = x.HeartbeatsPerMinute.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                        LastJobName = x.LastJobName,
                        LastJobOutcome = x.LastJobOutcome,
                        SuccessRate = FormatPercent(x.SuccessRate),
                        RunStarted = x.RunStarted,
                        RunSucceeded = x.RunSucceeded,
                        RunFailed = x.RunFailed,
                        RunRetried = x.RunRetried,
                        P95DurationMs = FormatNumberOrFallback(x.P95DurationMs)
                    })
                    .ToList()
            },
            FailureGroups = dashboard.FailureGroups
                .Select(x => new DashboardFailureGroupData
                {
                    TenantDisplayName = string.IsNullOrWhiteSpace(x.TenantDisplayName) ? "N/A" : x.TenantDisplayName,
                    JobName = string.IsNullOrWhiteSpace(x.JobName) ? "(unknown)" : x.JobName,
                    ErrorType = string.IsNullOrWhiteSpace(x.ErrorType) ? "N/A" : x.ErrorType,
                    ErrorMessage = string.IsNullOrWhiteSpace(x.ErrorMessage) ? "N/A" : x.ErrorMessage,
                    FailureCount = x.FailureCount,
                    FirstOccurredAtUtc = FormatUtc(x.FirstOccurredAtUtc),
                    LastOccurredAtUtc = FormatUtc(x.LastOccurredAtUtc),
                    WorkerName = string.IsNullOrWhiteSpace(x.WorkerName) ? "(unknown)" : x.WorkerName,
                    Attempt = x.Attempt?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "N/A",
                    DurationMs = FormatNumberOrFallback(x.DurationMs)
                })
                .ToList()
        };
    }

    public async Task<DashboardFailureDetailsResponse?> QueryFailureDetailsAsync(
        string? correlationId,
        string jobName,
        string errorType,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var preferenceValues = await _userPreferenceService.GetValuesAsync(PreferenceKeys.GlobalFilterKeys, cancellationToken);

        var query = new ReportDashboardFailureDetailsQueryRequest
        {
            Timeframe = MapTimeRangeToApiTimeframe(preferenceValues),
            TenantIds = ParseGuidList(preferenceValues, PreferenceKeys.GlobalFilterTenant, "all-tenants"),
            ProjectIds = ParseGuidList(preferenceValues, PreferenceKeys.GlobalFilterProject, "all-projects"),
            OrganizationIds = ParseGuidList(preferenceValues, PreferenceKeys.GlobalFilterOrganization, "all-organizations"),
            JobName = string.IsNullOrWhiteSpace(jobName) ? "(unknown)" : jobName.Trim(),
            ErrorType = string.IsNullOrWhiteSpace(errorType) ? "(unknown)" : errorType.Trim(),
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "(unknown)" : errorMessage.Trim()
        };

        var token = await _userApiTokenService.CreateReportsReadTokenAsync(cancellationToken);
        var details = await _reportsApiClient.GetFailureDetailsAsync(query, token, correlationId, cancellationToken);

        if (details is null)
        {
            return null;
        }

        return new DashboardFailureDetailsResponse
        {
            Timeframe = details.Timeframe,
            JobName = string.IsNullOrWhiteSpace(details.JobName) ? "(unknown)" : details.JobName,
            ErrorType = string.IsNullOrWhiteSpace(details.ErrorType) ? "N/A" : details.ErrorType,
            ErrorMessage = string.IsNullOrWhiteSpace(details.ErrorMessage) ? "N/A" : details.ErrorMessage,
            SampleCount = details.SampleCount,
            Samples = details.Samples
                .Select(x => new DashboardFailureSampleData
                {
                    TenantDisplayName = string.IsNullOrWhiteSpace(x.TenantDisplayName) ? "N/A" : x.TenantDisplayName,
                    OccurredAtUtc = FormatUtc(x.OccurredAtUtc),
                    WorkerName = string.IsNullOrWhiteSpace(x.WorkerName) ? "(unknown)" : x.WorkerName,
                    Attempt = x.Attempt?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "N/A",
                    RunId = x.RunId?.ToString("D") ?? "N/A",
                    ErrorDetail = string.IsNullOrWhiteSpace(x.ErrorDetail) ? "N/A" : x.ErrorDetail,
                    PayloadJson = string.IsNullOrWhiteSpace(x.PayloadJson) ? "N/A" : x.PayloadJson
                })
                .ToList()
        };
    }

    private static List<Guid> ParseGuidList(
        IReadOnlyDictionary<string, string> preferences,
        string key,
        string wildcard)
    {
        if (!preferences.TryGetValue(key, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        var trimmed = rawValue.Trim();
        if (string.Equals(trimmed, wildcard, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        if (Guid.TryParse(trimmed, out var parsed))
        {
            return [parsed];
        }

        return [];
    }

    private static string MapTimeRangeToApiTimeframe(IReadOnlyDictionary<string, string> preferences)
    {
        if (!preferences.TryGetValue(PreferenceKeys.GlobalFilterTimeRange, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return "last_24h";
        }

        return rawValue.Trim() switch
        {
            "1h" => "last_hour",
            "24h" => "last_24h",
            "7d" => "last_7d",
            "30d" => "last_30d",
            _ => "last_24h"
        };
    }

    private static string FormatPercent(double value)
    {
        return (value * 100d).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
    }

    private static string FormatNumberOrFallback(double? value)
    {
        if (!value.HasValue)
        {
            return "N/A";
        }

        return value.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatUtcOrFallback(DateTimeOffset? value)
    {
        return value.HasValue ? FormatUtc(value.Value) : "N/A";
    }

    private static string FormatUtc(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
    }
}
