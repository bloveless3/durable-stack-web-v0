using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DurableStack.Api.Services;

public sealed class TelemetryLifecycleMetrics : IDisposable
{
    private readonly Meter _meter = new("DurableStack.Api.TelemetryLifecycle", "1.0.0");

    private readonly Counter<long> _dashboardQueries;
    private readonly Histogram<double> _dashboardQueryDurationMs;
    private readonly Counter<long> _dashboardRawRowsScanned;
    private readonly Counter<long> _dashboardRollupBucketsRead;
    private readonly Counter<long> _dashboardFailureRollupGroupsRead;
    private readonly Counter<long> _dashboardCacheMisses;

    private readonly Counter<long> _rollupBucketsUpserted;
    private readonly Histogram<double> _rollupWatermarkLagMinutes;

    private readonly Counter<long> _retentionSkipped;
    private readonly Counter<long> _retentionRowsAffected;

    public TelemetryLifecycleMetrics()
    {
        _dashboardQueries = _meter.CreateCounter<long>("dashboard.query.count");
        _dashboardQueryDurationMs = _meter.CreateHistogram<double>("dashboard.query.duration.ms");
        _dashboardRawRowsScanned = _meter.CreateCounter<long>("dashboard.query.raw_rows_scanned");
        _dashboardRollupBucketsRead = _meter.CreateCounter<long>("dashboard.query.rollup_buckets_read");
        _dashboardFailureRollupGroupsRead = _meter.CreateCounter<long>("dashboard.query.failure_rollup_groups_read");
        _dashboardCacheMisses = _meter.CreateCounter<long>("dashboard.query.cache_miss");

        _rollupBucketsUpserted = _meter.CreateCounter<long>("rollup.buckets.upserted");
        _rollupWatermarkLagMinutes = _meter.CreateHistogram<double>("rollup.watermark.lag.minutes");

        _retentionSkipped = _meter.CreateCounter<long>("retention.run.skipped");
        _retentionRowsAffected = _meter.CreateCounter<long>("retention.rows.affected");
    }

    public void RecordDashboardQuery(
        string timeframe,
        bool hybridRequested,
        bool hybridApplied,
        double durationMs,
        long rawRowsScanned,
        long rollupBucketsRead,
        long failureRollupGroupsRead)
    {
        var tags = new TagList
        {
            { "timeframe", timeframe },
            { "hybrid_requested", hybridRequested },
            { "hybrid_applied", hybridApplied }
        };

        _dashboardQueries.Add(1, tags);
        _dashboardQueryDurationMs.Record(durationMs, tags);

        if (rawRowsScanned > 0)
        {
            _dashboardRawRowsScanned.Add(rawRowsScanned, tags);
        }

        if (rollupBucketsRead > 0)
        {
            _dashboardRollupBucketsRead.Add(rollupBucketsRead, tags);
        }

        if (failureRollupGroupsRead > 0)
        {
            _dashboardFailureRollupGroupsRead.Add(failureRollupGroupsRead, tags);
        }

        _dashboardCacheMisses.Add(1, tags);
    }

    public void RecordRollupBucketsUpserted(long bucketCount)
    {
        if (bucketCount <= 0)
        {
            return;
        }

        _rollupBucketsUpserted.Add(bucketCount);
    }

    public void RecordRollupWatermarkLag(string bucketSize, double lagMinutes)
    {
        if (lagMinutes < 0)
        {
            lagMinutes = 0;
        }

        _rollupWatermarkLagMinutes.Record(lagMinutes, new TagList { { "bucket_size", bucketSize } });
    }

    public void RecordRetentionSkipped(string reason)
    {
        _retentionSkipped.Add(1, new TagList { { "reason", reason } });
    }

    public void RecordRetentionRowsAffected(string table, long rowCount)
    {
        if (rowCount <= 0)
        {
            return;
        }

        _retentionRowsAffected.Add(rowCount, new TagList { { "table", table } });
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
