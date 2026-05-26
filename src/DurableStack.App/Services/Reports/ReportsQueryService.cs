using DurableStack.App.Models.Reports;
using DurableStack.App.Services.Api;
using DurableStack.App.Services.Preferences;
using DurableStack.Platform.Contracts;

namespace DurableStack.App.Services.Reports;

public interface IReportsQueryService
{
    Task<DashboardSummaryResponse?> QueryDashboardSummaryAsync(string? sinceCursor, string? correlationId, CancellationToken cancellationToken = default);
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

    public async Task<DashboardSummaryResponse?> QueryDashboardSummaryAsync(
        string? sinceCursor,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var preferenceValues = await _userPreferenceService.GetValuesAsync(PreferenceKeys.GlobalFilterKeys, cancellationToken);

        var reportQuery = new ReportSummaryQueryRequest
        {
            SinceCursor = sinceCursor,
            TenantIds = ParseGuidList(preferenceValues, PreferenceKeys.GlobalFilterTenant, "all-tenants"),
            ProjectIds = ParseGuidList(preferenceValues, PreferenceKeys.GlobalFilterProject, "all-projects"),
            OrganizationIds = ParseGuidList(preferenceValues, PreferenceKeys.GlobalFilterOrganization, "all-organizations")
        };

        var token = await _userApiTokenService.CreateReportsReadTokenAsync(cancellationToken);
        var summary = await _reportsApiClient.GetSummaryAsync(reportQuery, token, correlationId, cancellationToken);

        if (summary is null)
        {
            return null;
        }

        return new DashboardSummaryResponse
        {
            Status = "Connected",
            TotalEvents = summary.TotalEvents,
            FailedEvents = summary.FailedEvents,
            LastEventAtUtc = summary.LastEventAtUtc?.ToString("u") ?? "N/A",
            QueryRunAtUtc = summary.QueryRunAtUtc.ToString("u"),
            NextCursor = summary.NextCursor
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
}
