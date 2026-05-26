using Microsoft.Extensions.Options;

namespace DurableStack.Api.Services;

public sealed class LocalTelemetryLifecycleScheduler : BackgroundService
{
    private readonly ITelemetryRollupJob _rollupJob;
    private readonly ITelemetryRetentionJob _retentionJob;
    private readonly IOptionsMonitor<TelemetryLifecycleOptions> _options;
    private readonly ILogger<LocalTelemetryLifecycleScheduler> _logger;

    public LocalTelemetryLifecycleScheduler(
        ITelemetryRollupJob rollupJob,
        ITelemetryRetentionJob retentionJob,
        IOptionsMonitor<TelemetryLifecycleOptions> options,
        ILogger<LocalTelemetryLifecycleScheduler> logger)
    {
        _rollupJob = rollupJob;
        _retentionJob = retentionJob;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var executionMode = TelemetryLifecycleOptions.NormalizeExecutionMode(_options.CurrentValue.ExecutionMode);
        if (!string.Equals(executionMode, TelemetryLifecycleOptions.ExecutionModeLocal, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Telemetry lifecycle execution mode '{ExecutionMode}' is configured, but no DurableStack scheduler integration is wired yet in this solution. Lifecycle jobs will not run in this process.",
                executionMode);
            return;
        }

        _logger.LogWarning(
            "Telemetry lifecycle is running in local single-instance scheduler mode. This mode is not intended for multi-replica API deployments.");

        DateTimeOffset nextRollupAtUtc = DateTimeOffset.MinValue;
        DateTimeOffset nextRetentionAtUtc = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue;
            var rollupOptions = options.RollupWorker;
            var retentionOptions = options.RetentionWorker;
            var nowUtc = DateTimeOffset.UtcNow;

            if (rollupOptions.Enabled && nowUtc >= nextRollupAtUtc)
            {
                try
                {
                    await _rollupJob.RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Telemetry rollup execution failed.");
                }

                nextRollupAtUtc = nowUtc.AddMinutes(Math.Max(1, rollupOptions.IntervalMinutes));
            }

            if (retentionOptions.Enabled && nowUtc >= nextRetentionAtUtc)
            {
                try
                {
                    await _retentionJob.RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Telemetry retention execution failed.");
                }

                nextRetentionAtUtc = nowUtc.AddHours(Math.Max(1, retentionOptions.IntervalHours));
            }

            var soonestRunAtUtc = MinFuture(nextRollupAtUtc, nextRetentionAtUtc, nowUtc.AddMinutes(1));
            var delay = soonestRunAtUtc - DateTimeOffset.UtcNow;
            if (delay < TimeSpan.FromSeconds(1))
            {
                delay = TimeSpan.FromSeconds(1);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static DateTimeOffset MinFuture(DateTimeOffset left, DateTimeOffset right, DateTimeOffset fallback)
    {
        var candidates = new List<DateTimeOffset>(3);
        if (left > DateTimeOffset.MinValue)
        {
            candidates.Add(left);
        }

        if (right > DateTimeOffset.MinValue)
        {
            candidates.Add(right);
        }

        if (candidates.Count == 0)
        {
            return fallback;
        }

        return candidates.Min();
    }
}
