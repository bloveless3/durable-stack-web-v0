using DurableStack.Telemetry;
using DurableStack.Telemetry.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DurableStack.Api.Services;

public interface ITelemetryRollupJob
{
    Task RunOnceAsync(CancellationToken cancellationToken = default);
}

public sealed class TelemetryRollupJob : ITelemetryRollupJob
{
    private const string EventJobStarted = "job_started";
    private const string EventJobSucceeded = "job_succeeded";
    private const string EventJobFailed = "job_failed";
    private const string EventJobRetried = "job_retried";
    private const string EventWorkerHeartbeatBatch = "worker_heartbeat_batch";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TelemetryLifecycleOptions> _options;
    private readonly ILogger<TelemetryRollupJob> _logger;

    public TelemetryRollupJob(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<TelemetryLifecycleOptions> options,
        ILogger<TelemetryRollupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue.RollupWorker;
        if (!options.Enabled)
        {
            return;
        }

        await RunRollupsAsync(options, cancellationToken);
    }

    private async Task RunRollupsAsync(TelemetryRollupWorkerOptions options, CancellationToken cancellationToken)
    {
        var bucketSizes = options.BucketSizes
            .Select(TelemetryLifecycleTime.NormalizeBucketSize)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (bucketSizes.Count == 0)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var telemetryDb = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        var tenantPublicIds = await telemetryDb.TelemetryBatches
            .AsNoTracking()
            .Select(x => x.TenantPublicId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (tenantPublicIds.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var rolledUpBuckets = 0;

        foreach (var bucketSize in bucketSizes)
        {
            if (!TelemetryLifecycleTime.TryGetBucketInterval(bucketSize, out var bucketInterval))
            {
                continue;
            }

            var finalizationLag = TimeSpan.FromMinutes(Math.Max(1, options.FinalizationLagMinutes));
            var finalizedBefore = now - finalizationLag;
            var finalizedBoundaryBucket = TelemetryLifecycleTime.AlignToBucket(finalizedBefore, bucketInterval);

            foreach (var tenantPublicId in tenantPublicIds)
            {
                rolledUpBuckets += await RollupTenantBucketsAsync(
                    telemetryDb,
                    tenantPublicId,
                    bucketSize,
                    bucketInterval,
                    finalizedBoundaryBucket,
                    Math.Max(1, options.MaxBucketsPerTenantPerRun),
                    cancellationToken);
            }
        }

        if (rolledUpBuckets > 0)
        {
            _logger.LogInformation("Telemetry rollup worker upserted {BucketCount} bucket rollups.", rolledUpBuckets);
        }
    }

    private async Task<int> RollupTenantBucketsAsync(
        TelemetryDbContext telemetryDb,
        string tenantPublicId,
        string bucketSize,
        TimeSpan bucketInterval,
        DateTimeOffset finalizedBoundaryBucket,
        int maxBucketsPerRun,
        CancellationToken cancellationToken)
    {
        var watermark = await telemetryDb.TelemetryRollupWatermarks
            .SingleOrDefaultAsync(
                x => x.TenantPublicId == tenantPublicId && x.BucketSize == bucketSize,
                cancellationToken);

        DateTimeOffset? firstEventAtUtc = await telemetryDb.TelemetryEvents
            .AsNoTracking()
            .Where(x => x.Batch != null && x.Batch.TenantPublicId == tenantPublicId)
            .OrderBy(x => x.OccurredAtUtc)
            .Select(x => (DateTimeOffset?)x.OccurredAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (!firstEventAtUtc.HasValue)
        {
            return 0;
        }

        var nextBucketStart = watermark is null
            ? TelemetryLifecycleTime.AlignToBucket(firstEventAtUtc.Value, bucketInterval)
            : watermark.LastRolledUpBucketStartUtc.Add(bucketInterval);

        if (nextBucketStart >= finalizedBoundaryBucket)
        {
            return 0;
        }

        var computedAtUtc = DateTimeOffset.UtcNow;
        var processed = 0;

        while (nextBucketStart < finalizedBoundaryBucket && processed < maxBucketsPerRun)
        {
            var bucketEnd = nextBucketStart.Add(bucketInterval);

            var bucketRows = await telemetryDb.TelemetryEvents
                .AsNoTracking()
                .Where(x =>
                    x.Batch != null &&
                    x.Batch.TenantPublicId == tenantPublicId &&
                    x.OccurredAtUtc >= nextBucketStart &&
                    x.OccurredAtUtc < bucketEnd)
                .Select(x => new
                {
                    x.OccurredAtUtc,
                    x.EventType,
                    x.HeartbeatCount,
                    x.JobName,
                    x.ErrorType,
                    x.ErrorMessage
                })
                .ToListAsync(cancellationToken);

            var existingBucket = await telemetryDb.TelemetryBucketRollups
                .SingleOrDefaultAsync(
                    x => x.TenantPublicId == tenantPublicId &&
                         x.BucketSize == bucketSize &&
                         x.BucketStartUtc == nextBucketStart,
                    cancellationToken);

            if (existingBucket is null)
            {
                existingBucket = new TelemetryBucketRollup
                {
                    Id = Guid.NewGuid(),
                    TenantPublicId = tenantPublicId,
                    BucketSize = bucketSize,
                    BucketStartUtc = nextBucketStart
                };
                telemetryDb.TelemetryBucketRollups.Add(existingBucket);
            }

            existingBucket.RunStarted = bucketRows.Count(x => x.EventType == EventJobStarted);
            existingBucket.RunSucceeded = bucketRows.Count(x => x.EventType == EventJobSucceeded);
            existingBucket.RunFailed = bucketRows.Count(x => x.EventType == EventJobFailed);
            existingBucket.RunRetried = bucketRows.Count(x => x.EventType == EventJobRetried);
            existingBucket.HeartbeatCount = bucketRows
                .Where(x => x.EventType == EventWorkerHeartbeatBatch)
                .Sum(x => x.HeartbeatCount ?? 0);
            existingBucket.LastEventAtUtc = bucketRows.Count == 0
                ? null
                : bucketRows.Max(x => x.OccurredAtUtc);
            existingBucket.ComputedAtUtc = computedAtUtc;

            var existingFailureGroups = await telemetryDb.TelemetryFailureGroupRollups
                .Where(x =>
                    x.TenantPublicId == tenantPublicId &&
                    x.BucketSize == bucketSize &&
                    x.BucketStartUtc == nextBucketStart)
                .ToListAsync(cancellationToken);

            telemetryDb.TelemetryFailureGroupRollups.RemoveRange(existingFailureGroups);

            var failureGroups = bucketRows
                .Where(x => x.EventType == EventJobFailed)
                .GroupBy(x => new
                {
                    JobName = NormalizeFailureKey(x.JobName),
                    ErrorType = NormalizeFailureKey(x.ErrorType),
                    ErrorMessage = NormalizeFailureKey(x.ErrorMessage)
                })
                .Select(g => new TelemetryFailureGroupRollup
                {
                    Id = Guid.NewGuid(),
                    TenantPublicId = tenantPublicId,
                    BucketSize = bucketSize,
                    BucketStartUtc = nextBucketStart,
                    JobName = g.Key.JobName,
                    ErrorType = g.Key.ErrorType,
                    ErrorMessage = g.Key.ErrorMessage,
                    FailureCount = g.Count(),
                    FirstOccurredAtUtc = g.Min(x => x.OccurredAtUtc),
                    LastOccurredAtUtc = g.Max(x => x.OccurredAtUtc),
                    ComputedAtUtc = computedAtUtc
                })
                .ToList();

            if (failureGroups.Count > 0)
            {
                telemetryDb.TelemetryFailureGroupRollups.AddRange(failureGroups);
            }

            watermark ??= new TelemetryRollupWatermark
            {
                Id = Guid.NewGuid(),
                TenantPublicId = tenantPublicId,
                BucketSize = bucketSize,
                LastRolledUpBucketStartUtc = nextBucketStart,
                UpdatedAtUtc = computedAtUtc
            };

            watermark.LastRolledUpBucketStartUtc = nextBucketStart;
            watermark.UpdatedAtUtc = computedAtUtc;

            if (telemetryDb.Entry(watermark).State == EntityState.Detached)
            {
                telemetryDb.TelemetryRollupWatermarks.Add(watermark);
            }

            await telemetryDb.SaveChangesAsync(cancellationToken);

            processed++;
            nextBucketStart = nextBucketStart.Add(bucketInterval);
        }

        return processed;
    }

    private static string NormalizeFailureKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "(unknown)"
            : value.Trim();
    }
}
