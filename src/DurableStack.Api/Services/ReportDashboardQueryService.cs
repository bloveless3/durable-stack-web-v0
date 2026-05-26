using DurableStack.Platform.Contracts;
using DurableStack.Telemetry;
using DurableStack.Telemetry.Entities;
using Microsoft.EntityFrameworkCore;

namespace DurableStack.Api.Services;

public interface IReportDashboardQueryService
{
    Task<ReportDashboardResponse> QueryAsync(
        UserDashboardReportScope scope,
        IReadOnlyCollection<string> tenantPublicIds,
        IReadOnlyCollection<string> scopeTenantIds,
        IReadOnlyDictionary<string, string> tenantDisplayByPublicId,
        DateTimeOffset queryRunAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed class ReportDashboardQueryService : IReportDashboardQueryService
{
    private const string EventJobStarted = "job_started";
    private const string EventJobSucceeded = "job_succeeded";
    private const string EventJobFailed = "job_failed";
    private const string EventJobRetried = "job_retried";
    private const string EventWorkerHeartbeatBatch = "worker_heartbeat_batch";

    private readonly TelemetryDbContext _telemetryDb;

    public ReportDashboardQueryService(TelemetryDbContext telemetryDb)
    {
        _telemetryDb = telemetryDb;
    }

    public async Task<ReportDashboardResponse> QueryAsync(
        UserDashboardReportScope scope,
        IReadOnlyCollection<string> tenantPublicIds,
        IReadOnlyCollection<string> scopeTenantIds,
        IReadOnlyDictionary<string, string> tenantDisplayByPublicId,
        DateTimeOffset queryRunAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (tenantPublicIds.Count == 0)
        {
            return CreateEmpty(scope, scopeTenantIds, queryRunAtUtc);
        }

        var filteredEvents = _telemetryDb.TelemetryEvents
            .AsNoTracking()
            .Where(x =>
                x.Batch != null &&
                tenantPublicIds.Contains(x.Batch.TenantPublicId) &&
                x.OccurredAtUtc >= scope.WindowStartUtc &&
                x.OccurredAtUtc < scope.WindowEndUtc);

        var summaryAggregate = await filteredEvents
            .GroupBy(_ => 1)
            .Select(g => new
            {
                RunStarted = g.Count(x => x.EventType == EventJobStarted),
                RunSucceeded = g.Count(x => x.EventType == EventJobSucceeded),
                RunFailed = g.Count(x => x.EventType == EventJobFailed),
                RunRetried = g.Count(x => x.EventType == EventJobRetried),
                HeartbeatCount = g.Sum(x => x.EventType == EventWorkerHeartbeatBatch ? (x.HeartbeatCount ?? 0) : 0),
                LastEventAtUtc = g.Max(x => (DateTimeOffset?)x.OccurredAtUtc)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var runStarted = summaryAggregate?.RunStarted ?? 0;
        var runSucceeded = summaryAggregate?.RunSucceeded ?? 0;
        var runFailed = summaryAggregate?.RunFailed ?? 0;
        var runRetried = summaryAggregate?.RunRetried ?? 0;
        var heartbeatCount = summaryAggregate?.HeartbeatCount ?? 0;
        var runsTotal = runSucceeded + runFailed;

        var summary = new ReportDashboardSummary
        {
            RunStarted = runStarted,
            RunSucceeded = runSucceeded,
            RunFailed = runFailed,
            RunRetried = runRetried,
            RunsTotal = runsTotal,
            SuccessRate = runsTotal == 0 ? 0d : (double)runSucceeded / runsTotal,
            FailureRate = runsTotal == 0 ? 0d : (double)runFailed / runsTotal,
            RetryRate = runStarted == 0 ? 0d : (double)runRetried / runStarted,
            HeartbeatCount = heartbeatCount,
            LastEventAtUtc = summaryAggregate?.LastEventAtUtc
        };

        summary.P95DurationMs = await CalculateP95DurationMsAsync(filteredEvents, cancellationToken);

        var series = await BuildSeriesAsync(filteredEvents, scope, cancellationToken);
        var workers = await BuildWorkersAsync(filteredEvents, scope, queryRunAtUtc, tenantDisplayByPublicId, cancellationToken);
        var recentFailures = await filteredEvents
            .Where(x => x.EventType == EventJobFailed)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(50)
            .Select(x => new ReportDashboardFailureItem
            {
                OccurredAtUtc = x.OccurredAtUtc,
                JobName = x.JobName,
                WorkerName = x.WorkerName,
                RunId = x.RunId,
                Attempt = x.Attempt,
                ErrorType = x.ErrorType,
                ErrorMessage = x.ErrorMessage,
                DurationMs = x.DurationMs
            })
            .ToListAsync(cancellationToken);

        summary.ActiveWorkers = workers.StatusCounts.Online + workers.StatusCounts.Warn;

        return new ReportDashboardResponse
        {
            ScopeTenantIds = [.. scopeTenantIds],
            Timeframe = scope.Timeframe,
            WindowStartUtc = scope.WindowStartUtc,
            WindowEndUtc = scope.WindowEndUtc,
            BucketSize = scope.BucketSize,
            QueryRunAtUtc = queryRunAtUtc,
            Summary = summary,
            Series = series,
            Workers = workers,
            RecentFailures = recentFailures
        };
    }

    private static ReportDashboardResponse CreateEmpty(UserDashboardReportScope scope, IReadOnlyCollection<string> scopeTenantIds, DateTimeOffset queryRunAtUtc)
    {
        return new ReportDashboardResponse
        {
            ScopeTenantIds = [.. scopeTenantIds],
            Timeframe = scope.Timeframe,
            WindowStartUtc = scope.WindowStartUtc,
            WindowEndUtc = scope.WindowEndUtc,
            BucketSize = scope.BucketSize,
            QueryRunAtUtc = queryRunAtUtc,
            Summary = new ReportDashboardSummary(),
            Series = BuildEmptySeries(scope),
            Workers = new ReportDashboardWorkers(),
            RecentFailures = []
        };
    }

    private static List<ReportDashboardSeriesPoint> BuildEmptySeries(UserDashboardReportScope scope)
    {
        var points = new List<ReportDashboardSeriesPoint>();
        var bucketStart = AlignToBucket(scope.WindowStartUtc, scope.BucketInterval);
        var end = scope.WindowEndUtc;

        while (bucketStart < end)
        {
            points.Add(new ReportDashboardSeriesPoint { BucketStartUtc = bucketStart });
            bucketStart = bucketStart.Add(scope.BucketInterval);
        }

        return points;
    }

    private async Task<List<ReportDashboardSeriesPoint>> BuildSeriesAsync(
        IQueryable<TelemetryEvent> filteredEvents,
        UserDashboardReportScope scope,
        CancellationToken cancellationToken)
    {
        var raw = await filteredEvents
            .Where(x =>
                x.EventType == EventJobStarted ||
                x.EventType == EventJobSucceeded ||
                x.EventType == EventJobFailed ||
                x.EventType == EventJobRetried ||
                x.EventType == EventWorkerHeartbeatBatch)
            .Select(x => new { x.OccurredAtUtc, x.EventType, x.HeartbeatCount })
            .ToListAsync(cancellationToken);

        var buckets = new Dictionary<DateTimeOffset, ReportDashboardSeriesPoint>();
        var bucketStart = AlignToBucket(scope.WindowStartUtc, scope.BucketInterval);
        while (bucketStart < scope.WindowEndUtc)
        {
            buckets[bucketStart] = new ReportDashboardSeriesPoint { BucketStartUtc = bucketStart };
            bucketStart = bucketStart.Add(scope.BucketInterval);
        }

        foreach (var item in raw)
        {
            var itemBucket = AlignToBucket(item.OccurredAtUtc, scope.BucketInterval);
            if (!buckets.TryGetValue(itemBucket, out var point))
            {
                continue;
            }

            switch (item.EventType)
            {
                case EventJobStarted:
                    point.RunStarted++;
                    break;
                case EventJobSucceeded:
                    point.RunSucceeded++;
                    break;
                case EventJobFailed:
                    point.RunFailed++;
                    break;
                case EventJobRetried:
                    point.RunRetried++;
                    break;
                case EventWorkerHeartbeatBatch:
                    point.HeartbeatCount += item.HeartbeatCount ?? 0;
                    break;
            }
        }

        return buckets
            .OrderBy(x => x.Key)
            .Select(x => x.Value)
            .ToList();
    }

    private async Task<ReportDashboardWorkers> BuildWorkersAsync(
        IQueryable<TelemetryEvent> filteredEvents,
        UserDashboardReportScope scope,
        DateTimeOffset queryRunAtUtc,
        IReadOnlyDictionary<string, string> tenantDisplayByPublicId,
        CancellationToken cancellationToken)
    {
        var windowMinutes = Math.Max((scope.WindowEndUtc - scope.WindowStartUtc).TotalMinutes, 1);

        var workerAggregates = await filteredEvents
            .Where(x => x.WorkerName != null)
            .GroupBy(x => x.WorkerName!)
            .Select(g => new
            {
                WorkerName = g.Key,
                TenantPublicId = g
                    .Where(x => x.Batch != null)
                    .Select(x => x.Batch!.TenantPublicId)
                    .FirstOrDefault(),
                LastSeenAtUtc = g
                    .Where(x => x.EventType == EventWorkerHeartbeatBatch)
                    .Max(x => (DateTimeOffset?)x.OccurredAtUtc),
                HeartbeatCount = g
                    .Where(x => x.EventType == EventWorkerHeartbeatBatch)
                    .Sum(x => (int?)x.HeartbeatCount) ?? 0,
                RunStarted = g.Count(x => x.EventType == EventJobStarted),
                RunSucceeded = g.Count(x => x.EventType == EventJobSucceeded),
                RunFailed = g.Count(x => x.EventType == EventJobFailed),
                RunRetried = g.Count(x => x.EventType == EventJobRetried)
            })
            .ToListAsync(cancellationToken);

        var workerDurations = await filteredEvents
            .Where(x =>
                x.WorkerName != null &&
                (x.EventType == EventJobSucceeded || x.EventType == EventJobFailed) &&
                x.DurationMs.HasValue)
            .Select(x => new { WorkerName = x.WorkerName!, DurationMs = x.DurationMs!.Value })
            .ToListAsync(cancellationToken);

        var durationsByWorker = workerDurations
            .GroupBy(x => x.WorkerName, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x.Select(v => v.DurationMs).OrderBy(v => v).ToList(),
                StringComparer.Ordinal);

        var workerLastJobs = await filteredEvents
            .Where(x =>
                x.WorkerName != null &&
                (x.EventType == EventJobSucceeded || x.EventType == EventJobFailed))
            .Select(x => new { WorkerName = x.WorkerName!, x.OccurredAtUtc, x.JobName, x.EventType })
            .ToListAsync(cancellationToken);

        var lastJobByWorker = workerLastJobs
            .GroupBy(x => x.WorkerName, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(v => v.OccurredAtUtc).First(),
                StringComparer.Ordinal);

        var items = new List<ReportDashboardWorkerItem>(workerAggregates.Count);

        foreach (var aggregate in workerAggregates)
        {
            if (!aggregate.LastSeenAtUtc.HasValue)
            {
                continue;
            }

            var freshnessSeconds = (int)Math.Max(0, (queryRunAtUtc - aggregate.LastSeenAtUtc.Value).TotalSeconds);
            var status = freshnessSeconds <= 20
                ? "online"
                : freshnessSeconds <= 60
                    ? "warn"
                    : "offline";

            var runsTotal = aggregate.RunSucceeded + aggregate.RunFailed;
            var successRate = runsTotal == 0 ? 0d : (double)aggregate.RunSucceeded / runsTotal;

            lastJobByWorker.TryGetValue(aggregate.WorkerName, out var lastJob);

            double? workerP95 = null;
            if (durationsByWorker.TryGetValue(aggregate.WorkerName, out var durations) && durations.Count > 0)
            {
                workerP95 = CalculatePercentileFromSorted(durations, 0.95);
            }

            items.Add(new ReportDashboardWorkerItem
            {
                WorkerName = aggregate.WorkerName,
                TenantDisplayName = ResolveTenantDisplayName(aggregate.TenantPublicId, tenantDisplayByPublicId),
                Status = status,
                LastSeenAtUtc = aggregate.LastSeenAtUtc.Value,
                FreshnessSeconds = freshnessSeconds,
                HeartbeatsPerMinute = aggregate.HeartbeatCount / windowMinutes,
                LastJobName = lastJob?.JobName,
                LastJobOutcome = lastJob is null
                    ? null
                    : lastJob.EventType == EventJobSucceeded ? "succeeded" : "failed",
                SuccessRate = successRate,
                RunStarted = aggregate.RunStarted,
                RunSucceeded = aggregate.RunSucceeded,
                RunFailed = aggregate.RunFailed,
                RunRetried = aggregate.RunRetried,
                P95DurationMs = workerP95
            });
        }

        var ordered = items
            .OrderBy(x => x.Status == "offline" ? 0 : x.Status == "warn" ? 1 : 2)
            .ThenBy(x => x.FreshnessSeconds)
            .ThenBy(x => x.WorkerName, StringComparer.Ordinal)
            .ToList();

        return new ReportDashboardWorkers
        {
            StatusCounts = new ReportDashboardWorkerStatusCounts
            {
                Online = ordered.Count(x => x.Status == "online"),
                Warn = ordered.Count(x => x.Status == "warn"),
                Offline = ordered.Count(x => x.Status == "offline")
            },
            Items = ordered
        };
    }

    private static string? ResolveTenantDisplayName(string? tenantPublicId, IReadOnlyDictionary<string, string> tenantDisplayByPublicId)
    {
        if (string.IsNullOrWhiteSpace(tenantPublicId))
        {
            return null;
        }

        return tenantDisplayByPublicId.TryGetValue(tenantPublicId, out var displayName)
            ? displayName
            : null;
    }

    private async Task<double?> CalculateP95DurationMsAsync(
        IQueryable<TelemetryEvent> filteredEvents,
        CancellationToken cancellationToken)
    {
        var terminalDurations = filteredEvents
            .Where(x =>
                (x.EventType == EventJobSucceeded || x.EventType == EventJobFailed) &&
                x.DurationMs.HasValue)
            .Select(x => x.DurationMs!.Value);

        var count = await terminalDurations.CountAsync(cancellationToken);
        if (count == 0)
        {
            return null;
        }

        var index = (int)Math.Ceiling(count * 0.95) - 1;
        if (index < 0)
        {
            index = 0;
        }

        var value = await terminalDurations
            .OrderBy(x => x)
            .Skip(index)
            .Select(x => (double?)x)
            .FirstOrDefaultAsync(cancellationToken);

        return value;
    }

    private static DateTimeOffset AlignToBucket(DateTimeOffset value, TimeSpan bucketInterval)
    {
        var ticks = value.UtcDateTime.Ticks;
        var bucketTicks = bucketInterval.Ticks;
        var alignedTicks = ticks - (ticks % bucketTicks);
        return new DateTimeOffset(alignedTicks, TimeSpan.Zero);
    }

    private static double CalculatePercentileFromSorted(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(sortedValues.Count * percentile) - 1;
        if (index < 0)
        {
            index = 0;
        }

        if (index >= sortedValues.Count)
        {
            index = sortedValues.Count - 1;
        }

        return sortedValues[index];
    }

}
