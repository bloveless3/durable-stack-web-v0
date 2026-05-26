using DurableStack.Api.Services;
using DurableStack.Telemetry;
using DurableStack.Telemetry.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DurableStack.Api.Tests;

public sealed class TelemetryRetentionJobTests
{
    [Fact]
    public async Task RunOnceAsync_WhenWatermarkNotReady_DoesNotDeleteRawRows()
    {
        var services = new ServiceCollection();
        var dbName = $"retention-{Guid.NewGuid():N}";

        services.AddDbContext<TelemetryDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddSingleton<TelemetryLifecycleMetrics>();
        services.AddSingleton<IOptionsMonitor<TelemetryLifecycleOptions>>(
            new StaticOptionsMonitor<TelemetryLifecycleOptions>(new TelemetryLifecycleOptions
            {
                RetentionWorker = new TelemetryRetentionWorkerOptions
                {
                    Enabled = true,
                    DryRun = false,
                    FreeRetentionDays = 1,
                    PaidRetentionDays = 730
                }
            }));
        services.AddSingleton<ITelemetryRetentionJob, TelemetryRetentionJob>();
        services.AddSingleton(typeof(ILogger<TelemetryRetentionJob>), NullLogger<TelemetryRetentionJob>.Instance);

        using var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
            var nowUtc = DateTimeOffset.UtcNow;

            var batch = new TelemetryBatch
            {
                Id = Guid.NewGuid(),
                TenantPublicId = "tenant_retention_test",
                ReceivedAtUtc = nowUtc.AddDays(-10),
                AcceptedCount = 1,
                RejectedCount = 0
            };

            batch.Events.Add(new TelemetryEvent
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                EventType = "job_failed",
                EventVersion = 2,
                OccurredAtUtc = nowUtc.AddDays(-10),
                JobName = "retention-job",
                WorkerName = "worker-retention",
                ErrorType = "TimeoutException",
                ErrorMessage = "slow"
            });

            db.TelemetryBatches.Add(batch);

            db.TelemetryRollupWatermarks.Add(new TelemetryRollupWatermark
            {
                Id = Guid.NewGuid(),
                TenantPublicId = "tenant_retention_test",
                BucketSize = "15m",
                LastRolledUpBucketStartUtc = nowUtc.AddDays(-12),
                UpdatedAtUtc = nowUtc
            });

            await db.SaveChangesAsync();
        }

        var job = provider.GetRequiredService<ITelemetryRetentionJob>();
        await job.RunOnceAsync();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
            Assert.Equal(1, await db.TelemetryBatches.CountAsync());
            Assert.Equal(1, await db.TelemetryEvents.CountAsync());
        }
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener)
        {
            return null;
        }
    }
}
