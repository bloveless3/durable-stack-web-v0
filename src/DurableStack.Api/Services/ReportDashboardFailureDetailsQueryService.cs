using DurableStack.Platform.Contracts;
using DurableStack.Telemetry;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DurableStack.Api.Services;

public interface IReportDashboardFailureDetailsQueryService
{
    Task<ReportDashboardFailureDetailsResponse> QueryAsync(
        UserDashboardReportScope scope,
        IReadOnlyCollection<string> tenantPublicIds,
        IReadOnlyDictionary<string, string> tenantDisplayByPublicId,
        string jobName,
        string errorType,
        string errorMessage,
        CancellationToken cancellationToken = default);
}

public sealed class ReportDashboardFailureDetailsQueryService : IReportDashboardFailureDetailsQueryService
{
    private readonly TelemetryDbContext _telemetryDb;

    public ReportDashboardFailureDetailsQueryService(TelemetryDbContext telemetryDb)
    {
        _telemetryDb = telemetryDb;
    }

    public async Task<ReportDashboardFailureDetailsResponse> QueryAsync(
        UserDashboardReportScope scope,
        IReadOnlyCollection<string> tenantPublicIds,
        IReadOnlyDictionary<string, string> tenantDisplayByPublicId,
        string jobName,
        string errorType,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var normalizedJobName = NormalizeRequestedFailureKey(jobName);
        var normalizedErrorType = NormalizeRequestedFailureKey(errorType);
        var normalizedErrorMessage = NormalizeRequestedFailureKey(errorMessage);

        if (tenantPublicIds.Count == 0)
        {
            return new ReportDashboardFailureDetailsResponse
            {
                Timeframe = scope.Timeframe,
                JobName = normalizedJobName,
                ErrorType = normalizedErrorType,
                ErrorMessage = normalizedErrorMessage,
                SampleCount = 0
            };
        }

        var samples = await _telemetryDb.TelemetryEvents
            .AsNoTracking()
            .Where(x =>
                x.Batch != null &&
                tenantPublicIds.Contains(x.Batch.TenantPublicId) &&
                x.EventType == "job_failed" &&
                x.OccurredAtUtc >= scope.WindowStartUtc &&
                x.OccurredAtUtc < scope.WindowEndUtc)
            .Where(x =>
                (((x.JobName ?? string.Empty).Trim() == string.Empty ? "(unknown)" : (x.JobName ?? string.Empty).Trim()) == normalizedJobName)
                && (((x.ErrorType ?? string.Empty).Trim() == string.Empty ? "(unknown)" : (x.ErrorType ?? string.Empty).Trim()) == normalizedErrorType)
                && (((x.ErrorMessage ?? string.Empty).Trim() == string.Empty ? "(unknown)" : (x.ErrorMessage ?? string.Empty).Trim()) == normalizedErrorMessage))
            .OrderByDescending(x => x.OccurredAtUtc)
            .Select(x => new
            {
                TenantPublicId = x.Batch != null ? x.Batch.TenantPublicId : null,
                x.OccurredAtUtc,
                x.WorkerName,
                x.Attempt,
                x.RunId,
                x.PayloadJson
            })
            .Take(10)
            .ToListAsync(cancellationToken);

        return new ReportDashboardFailureDetailsResponse
        {
            Timeframe = scope.Timeframe,
            JobName = normalizedJobName,
            ErrorType = normalizedErrorType,
            ErrorMessage = normalizedErrorMessage,
            SampleCount = samples.Count,
            Samples = samples
                .Select(x => new ReportDashboardFailureSampleItem
                {
                    TenantDisplayName = ResolveTenantDisplayName(x.TenantPublicId, tenantDisplayByPublicId),
                    OccurredAtUtc = x.OccurredAtUtc,
                    WorkerName = string.IsNullOrWhiteSpace(x.WorkerName) ? "(unknown)" : x.WorkerName,
                    Attempt = x.Attempt,
                    RunId = x.RunId,
                    ErrorDetail = ExtractErrorDetail(x.PayloadJson),
                    PayloadJson = string.IsNullOrWhiteSpace(x.PayloadJson) ? null : x.PayloadJson
                })
                .ToList()
        };
    }

    private static string? ExtractErrorDetail(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (!document.RootElement.TryGetProperty("errorDetail", out var detailElement))
            {
                return null;
            }

            var detail = detailElement.GetString();
            return string.IsNullOrWhiteSpace(detail) ? null : detail;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string NormalizeFailureKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "(unknown)"
            : value.Trim();
    }

    private static string NormalizeRequestedFailureKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(unknown)";
        }

        var normalized = value.Trim();
        if (string.Equals(normalized, "N/A", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "(unknown)", StringComparison.OrdinalIgnoreCase))
        {
            return "(unknown)";
        }

        return normalized;
    }

    private static string ResolveTenantDisplayName(string? tenantPublicId, IReadOnlyDictionary<string, string> tenantDisplayByPublicId)
    {
        if (string.IsNullOrWhiteSpace(tenantPublicId))
        {
            return "N/A";
        }

        return tenantDisplayByPublicId.TryGetValue(tenantPublicId, out var displayName)
            ? displayName
            : tenantPublicId;
    }
}
