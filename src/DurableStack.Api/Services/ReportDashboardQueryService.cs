using DurableStack.Platform.Contracts;
using DurableStack.Telemetry;
using DurableStack.Telemetry.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;

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
    private readonly IOptionsMonitor<TelemetryLifecycleOptions> _lifecycleOptions;
    private readonly TelemetryLifecycleMetrics _metrics;

    public ReportDashboardQueryService(
        TelemetryDbContext telemetryDb,
        IOptionsMonitor<TelemetryLifecycleOptions> lifecycleOptions,
        TelemetryLifecycleMetrics metrics)
    {
        _telemetryDb = telemetryDb;
        _lifecycleOptions = lifecycleOptions;
        _metrics = metrics;
    }

    public async Task<ReportDashboardResponse> QueryAsync(
        UserDashboardReportScope scope,
        IReadOnlyCollection<string> tenantPublicIds,
        IReadOnlyCollection<string> scopeTenantIds,
        IReadOnlyDictionary<string, string> tenantDisplayByPublicId,
        DateTimeOffset queryRunAtUtc,
        CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();

        if (tenantPublicIds.Count == 0)
        {
            _metrics.RecordDashboardQuery(scope.Timeframe, hybridRequested: false, hybridApplied: false, durationMs: timer.Elapsed.TotalMilliseconds, rawRowsScanned: 0, rollupBucketsRead: 0, failureRollupGroupsRead: 0);
            return CreateEmpty(scope, scopeTenantIds, queryRunAtUtc);
        }

        var filteredEvents = _telemetryDb.TelemetryEvents
            .AsNoTracking()
            .Where(x =>
                x.Batch != null &&
                tenantPublicIds.Contains(x.Batch.TenantPublicId) &&
                x.OccurredAtUtc >= scope.WindowStartUtc &&
                x.OccurredAtUtc < scope.WindowEndUtc);

        var hybridRequested = _lifecycleOptions.CurrentValue.Query.EnableHybridRollupReads &&
                            !string.Equals(scope.Timeframe, "last_hour", StringComparison.Ordinal);
        var hybridEnabled = hybridRequested;

        var rawRecentWindowStartUtc = scope.WindowStartUtc;
        var rollupWindowStartUtc = scope.WindowStartUtc;
        var rollupWindowEndUtc = scope.WindowStartUtc;

        if (hybridEnabled)
        {
            var boundaryUtc = scope.WindowEndUtc.AddHours(-1);
            if (boundaryUtc > scope.WindowStartUtc)
            {
                var boundaryBucketUtc = AlignToBucket(boundaryUtc, scope.BucketInterval);
                rawRecentWindowStartUtc = boundaryBucketUtc;
                rollupWindowEndUtc = boundaryBucketUtc;
            }
            else
            {
                hybridEnabled = false;
            }
        }

        var rawRecentEvents = filteredEvents.Where(x => x.OccurredAtUtc >= rawRecentWindowStartUtc);

        List<TelemetryBucketRollup> rollupRows = [];
        List<TelemetryFailureGroupRollup> failureRollupRows = [];

        if (hybridEnabled)
        {
            rollupRows = await _telemetryDb.TelemetryBucketRollups
                .AsNoTracking()
                .Where(x =>
                    tenantPublicIds.Contains(x.TenantPublicId) &&
                    x.BucketSize == scope.BucketSize &&
                    x.BucketStartUtc >= rollupWindowStartUtc &&
                    x.BucketStartUtc < rollupWindowEndUtc)
                .ToListAsync(cancellationToken);

            failureRollupRows = await _telemetryDb.TelemetryFailureGroupRollups
                .AsNoTracking()
                .Where(x =>
                    tenantPublicIds.Contains(x.TenantPublicId) &&
                    x.BucketSize == scope.BucketSize &&
                    x.BucketStartUtc >= rollupWindowStartUtc &&
                    x.BucketStartUtc < rollupWindowEndUtc)
                .ToListAsync(cancellationToken);

            var historicalRawExists = await filteredEvents
                .Where(x => x.OccurredAtUtc < rawRecentWindowStartUtc)
                .AnyAsync(cancellationToken);

            if (historicalRawExists && rollupRows.Count == 0)
            {
                hybridEnabled = false;
                rawRecentWindowStartUtc = scope.WindowStartUtc;
                rawRecentEvents = filteredEvents;
                rollupRows = [];
                failureRollupRows = [];
            }
        }

        var summaryAggregate = await rawRecentEvents
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

        long rawRowsScanned = await rawRecentEvents
            .Where(x =>
                x.EventType == EventJobStarted ||
                x.EventType == EventJobSucceeded ||
                x.EventType == EventJobFailed ||
                x.EventType == EventJobRetried ||
                x.EventType == EventWorkerHeartbeatBatch)
            .LongCountAsync(cancellationToken);

        var runStarted = (summaryAggregate?.RunStarted ?? 0) + rollupRows.Sum(x => x.RunStarted);
        var runSucceeded = (summaryAggregate?.RunSucceeded ?? 0) + rollupRows.Sum(x => x.RunSucceeded);
        var runFailed = (summaryAggregate?.RunFailed ?? 0) + rollupRows.Sum(x => x.RunFailed);
        var runRetried = (summaryAggregate?.RunRetried ?? 0) + rollupRows.Sum(x => x.RunRetried);
        var heartbeatCount = (summaryAggregate?.HeartbeatCount ?? 0) + rollupRows.Sum(x => x.HeartbeatCount);
        var runsTotal = runSucceeded + runFailed;
        var rollupLastEventAtUtc = rollupRows
            .Where(x => x.LastEventAtUtc.HasValue)
            .Select(x => x.LastEventAtUtc)
            .OrderByDescending(x => x)
            .FirstOrDefault();

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
            LastEventAtUtc = Max(summaryAggregate?.LastEventAtUtc, rollupLastEventAtUtc)
        };

        summary.P95DurationMs = await CalculateP95DurationMsAsync(filteredEvents, cancellationToken);

        var series = await BuildSeriesAsync(rawRecentEvents, rollupRows, scope, cancellationToken);
        var workers = await BuildWorkersAsync(filteredEvents, scope, queryRunAtUtc, tenantDisplayByPublicId, cancellationToken);
        var failureRows = await rawRecentEvents
            .Where(x => x.EventType == EventJobFailed)
            .Select(x => new
            {
                x.OccurredAtUtc,
                x.JobName,
                x.WorkerName,
                x.Attempt,
                x.ErrorType,
                x.ErrorMessage,
                x.DurationMs,
                TenantPublicId = x.Batch != null ? x.Batch.TenantPublicId : null
            })
            .ToListAsync(cancellationToken);

        var failureGroupMap = new Dictionary<(string TenantPublicId, string JobName, string ErrorType, string ErrorMessage), ReportDashboardFailureGroupItem>();

        foreach (var rawGroup in failureRows
            .GroupBy(x => new
            {
                TenantPublicId = NormalizeFailureKey(x.TenantPublicId),
                JobName = NormalizeFailureKey(x.JobName),
                ErrorType = NormalizeFailureKey(x.ErrorType),
                ErrorMessage = NormalizeFailureKey(x.ErrorMessage)
            }))
        {
            var latest = rawGroup
                .OrderByDescending(x => x.OccurredAtUtc)
                .First();

            failureGroupMap[(rawGroup.Key.TenantPublicId, rawGroup.Key.JobName, rawGroup.Key.ErrorType, rawGroup.Key.ErrorMessage)] = new ReportDashboardFailureGroupItem
            {
                TenantDisplayName = ResolveTenantDisplayName(rawGroup.Key.TenantPublicId, tenantDisplayByPublicId),
                JobName = latest.JobName,
                ErrorType = latest.ErrorType,
                ErrorMessage = latest.ErrorMessage,
                FailureCount = rawGroup.Count(),
                FirstOccurredAtUtc = rawGroup.Min(x => x.OccurredAtUtc),
                LastOccurredAtUtc = rawGroup.Max(x => x.OccurredAtUtc),
                WorkerName = latest.WorkerName,
                Attempt = latest.Attempt,
                DurationMs = latest.DurationMs
            };
        }

        foreach (var rollup in failureRollupRows)
        {
            var key = (
                NormalizeFailureKey(rollup.TenantPublicId),
                NormalizeFailureKey(rollup.JobName),
                NormalizeFailureKey(rollup.ErrorType),
                NormalizeFailureKey(rollup.ErrorMessage));

            if (failureGroupMap.TryGetValue(key, out var existing))
            {
                existing.FailureCount += rollup.FailureCount;
                existing.FirstOccurredAtUtc = Min(existing.FirstOccurredAtUtc, rollup.FirstOccurredAtUtc);
                existing.LastOccurredAtUtc = Max(existing.LastOccurredAtUtc, rollup.LastOccurredAtUtc);
                existing.TenantDisplayName ??= ResolveTenantDisplayName(rollup.TenantPublicId, tenantDisplayByPublicId);
            }
            else
            {
                failureGroupMap[key] = new ReportDashboardFailureGroupItem
                {
                    TenantDisplayName = ResolveTenantDisplayName(rollup.TenantPublicId, tenantDisplayByPublicId),
                    JobName = rollup.JobName,
                    ErrorType = rollup.ErrorType,
                    ErrorMessage = rollup.ErrorMessage,
                    FailureCount = rollup.FailureCount,
                    FirstOccurredAtUtc = rollup.FirstOccurredAtUtc,
                    LastOccurredAtUtc = rollup.LastOccurredAtUtc
                };
            }
        }

        var failureGroups = failureGroupMap
            .Values
            .OrderByDescending(x => x.FailureCount)
            .ThenByDescending(x => x.LastOccurredAtUtc)
            .Take(50)
            .ToList();

        summary.ActiveWorkers = workers.StatusCounts.Online + workers.StatusCounts.Warn;

        _metrics.RecordDashboardQuery(
            scope.Timeframe,
            hybridRequested,
            hybridEnabled,
            timer.Elapsed.TotalMilliseconds,
            rawRowsScanned,
            rollupRows.Count,
            failureRollupRows.Count);

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
            FailureGroups = failureGroups
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
            FailureGroups = []
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
        IReadOnlyCollection<TelemetryBucketRollup> rollupRows,
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

        foreach (var rollup in rollupRows)
        {
            if (!buckets.TryGetValue(rollup.BucketStartUtc, out var point))
            {
                continue;
            }

            point.RunStarted += rollup.RunStarted;
            point.RunSucceeded += rollup.RunSucceeded;
            point.RunFailed += rollup.RunFailed;
            point.RunRetried += rollup.RunRetried;
            point.HeartbeatCount += rollup.HeartbeatCount;
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

    private static string NormalizeFailureKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "(unknown)"
            : value.Trim();
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right)
    {
        return left <= right ? left : right;
    }

    private static DateTimeOffset? Min(DateTimeOffset? left, DateTimeOffset? right)
    {
        if (!left.HasValue)
        {
            return right;
        }

        if (!right.HasValue)
        {
            return left;
        }

        return left.Value <= right.Value ? left : right;
    }

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right)
    {
        return left >= right ? left : right;
    }

    private static DateTimeOffset? Max(DateTimeOffset? left, DateTimeOffset? right)
    {
        if (!left.HasValue)
        {
            return right;
        }

        if (!right.HasValue)
        {
            return left;
        }

        return left.Value >= right.Value ? left : right;
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
