using DurableStack.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DurableStack.Api.Services;

public interface ITelemetryRetentionJob
{
    Task RunOnceAsync(CancellationToken cancellationToken = default);
}

public sealed class TelemetryRetentionJob : ITelemetryRetentionJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TelemetryLifecycleOptions> _options;
    private readonly ILogger<TelemetryRetentionJob> _logger;

    public TelemetryRetentionJob(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<TelemetryLifecycleOptions> options,
        ILogger<TelemetryRetentionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue.RetentionWorker;
        if (!options.Enabled)
        {
            return;
        }

        await RunRetentionAsync(options, cancellationToken);
    }

    private async Task RunRetentionAsync(TelemetryRetentionWorkerOptions options, CancellationToken cancellationToken)
    {
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

        var paidTenantSet = new HashSet<string>(
            options.PaidTenantPublicIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()),
            StringComparer.Ordinal);

        var runAtUtc = DateTimeOffset.UtcNow;

        foreach (var tenantPublicId in tenantPublicIds)
        {
            var retentionDays = paidTenantSet.Contains(tenantPublicId)
                ? Math.Max(1, options.PaidRetentionDays)
                : Math.Max(1, options.FreeRetentionDays);

            var cutoffUtc = runAtUtc.AddDays(-retentionDays);

            var bucket15mCutoff = GetBucketCutoff(cutoffUtc, "15m");
            if (!await HasRollupCoverageAsync(telemetryDb, tenantPublicId, "15m", bucket15mCutoff, cancellationToken))
            {
                _logger.LogWarning(
                    "Skipping retention for tenant {TenantPublicId} because watermark is not caught up to cutoff {CutoffUtc}.",
                    tenantPublicId,
                    cutoffUtc);
                continue;
            }

            var rawDeleteCount = await telemetryDb.TelemetryEvents
                .Where(x => x.Batch != null && x.Batch.TenantPublicId == tenantPublicId && x.OccurredAtUtc < cutoffUtc)
                .CountAsync(cancellationToken);

            var rawBatchDeleteCount = await telemetryDb.TelemetryBatches
                .Where(x => x.TenantPublicId == tenantPublicId && x.ReceivedAtUtc < cutoffUtc)
                .CountAsync(cancellationToken);

            var rollupDeleteCount = await telemetryDb.TelemetryBucketRollups
                .Where(x => x.TenantPublicId == tenantPublicId && x.BucketStartUtc < cutoffUtc)
                .CountAsync(cancellationToken);

            var failureRollupDeleteCount = await telemetryDb.TelemetryFailureGroupRollups
                .Where(x => x.TenantPublicId == tenantPublicId && x.BucketStartUtc < cutoffUtc)
                .CountAsync(cancellationToken);

            if (options.DryRun)
            {
                _logger.LogInformation(
                    "Retention dry-run tenant={TenantPublicId} retentionDays={RetentionDays} cutoff={CutoffUtc} rawEvents={RawEventCount} rawBatches={RawBatchCount} rollups={RollupCount} failureRollups={FailureRollupCount}.",
                    tenantPublicId,
                    retentionDays,
                    cutoffUtc,
                    rawDeleteCount,
                    rawBatchDeleteCount,
                    rollupDeleteCount,
                    failureRollupDeleteCount);
                continue;
            }

            if (rawDeleteCount > 0)
            {
                await telemetryDb.TelemetryEvents
                    .Where(x => x.Batch != null && x.Batch.TenantPublicId == tenantPublicId && x.OccurredAtUtc < cutoffUtc)
                    .ExecuteDeleteAsync(cancellationToken);
            }

            if (rawBatchDeleteCount > 0)
            {
                await telemetryDb.TelemetryBatches
                    .Where(x => x.TenantPublicId == tenantPublicId && x.ReceivedAtUtc < cutoffUtc)
                    .ExecuteDeleteAsync(cancellationToken);
            }

            if (rollupDeleteCount > 0)
            {
                await telemetryDb.TelemetryBucketRollups
                    .Where(x => x.TenantPublicId == tenantPublicId && x.BucketStartUtc < cutoffUtc)
                    .ExecuteDeleteAsync(cancellationToken);
            }

            if (failureRollupDeleteCount > 0)
            {
                await telemetryDb.TelemetryFailureGroupRollups
                    .Where(x => x.TenantPublicId == tenantPublicId && x.BucketStartUtc < cutoffUtc)
                    .ExecuteDeleteAsync(cancellationToken);
            }

            _logger.LogInformation(
                "Retention applied tenant={TenantPublicId} retentionDays={RetentionDays} cutoff={CutoffUtc} deleted rawEvents={RawEventCount}, rawBatches={RawBatchCount}, rollups={RollupCount}, failureRollups={FailureRollupCount}.",
                tenantPublicId,
                retentionDays,
                cutoffUtc,
                rawDeleteCount,
                rawBatchDeleteCount,
                rollupDeleteCount,
                failureRollupDeleteCount);
        }
    }

    private static DateTimeOffset GetBucketCutoff(DateTimeOffset cutoffUtc, string bucketSize)
    {
        if (!TelemetryLifecycleTime.TryGetBucketInterval(bucketSize, out var interval))
        {
            return cutoffUtc;
        }

        return TelemetryLifecycleTime.AlignToBucket(cutoffUtc, interval);
    }

    private static async Task<bool> HasRollupCoverageAsync(
        TelemetryDbContext telemetryDb,
        string tenantPublicId,
        string bucketSize,
        DateTimeOffset cutoffBucketStart,
        CancellationToken cancellationToken)
    {
        var watermark = await telemetryDb.TelemetryRollupWatermarks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.TenantPublicId == tenantPublicId && x.BucketSize == bucketSize,
                cancellationToken);

        return watermark is not null && watermark.LastRolledUpBucketStartUtc >= cutoffBucketStart;
    }
}
