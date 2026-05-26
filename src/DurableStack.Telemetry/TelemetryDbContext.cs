using DurableStack.Telemetry.Entities;
using Microsoft.EntityFrameworkCore;

namespace DurableStack.Telemetry;

public sealed class TelemetryDbContext : DbContext
{
    public TelemetryDbContext(DbContextOptions<TelemetryDbContext> options)
        : base(options)
    {
    }

    public DbSet<TelemetryBatch> TelemetryBatches => Set<TelemetryBatch>();

    public DbSet<TelemetryEvent> TelemetryEvents => Set<TelemetryEvent>();

    public DbSet<TelemetryBucketRollup> TelemetryBucketRollups => Set<TelemetryBucketRollup>();

    public DbSet<TelemetryFailureGroupRollup> TelemetryFailureGroupRollups => Set<TelemetryFailureGroupRollup>();

    public DbSet<TelemetryRollupWatermark> TelemetryRollupWatermarks => Set<TelemetryRollupWatermark>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TelemetryBatch>(entity =>
        {
            entity.ToTable("telemetry_batches");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantPublicId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ServiceName).HasMaxLength(200);
            entity.Property(x => x.EnvironmentName).HasMaxLength(100);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(200);
            entity.Property(x => x.ReceivedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.TenantPublicId, x.ReceivedAtUtc });
            entity.HasIndex(x => new { x.TenantPublicId, x.IdempotencyKey }).IsUnique();
        });

        modelBuilder.Entity<TelemetryEvent>(entity =>
        {
            entity.ToTable("telemetry_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EventVersion).IsRequired();
            entity.Property(x => x.OccurredAtUtc).IsRequired();
            entity.Property(x => x.JobName).HasMaxLength(200);
            entity.Property(x => x.WorkerName).HasMaxLength(200);
            entity.Property(x => x.ErrorType).HasMaxLength(200);
            entity.Property(x => x.ErrorMessage).HasMaxLength(4000);
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb");
            entity.Property(x => x.HeartbeatCount);
            entity.HasOne(x => x.Batch)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.EventType, x.OccurredAtUtc });
            entity.HasIndex(x => x.RunId);
            entity.HasIndex(x => new { x.OccurredAtUtc, x.BatchId });
            entity.HasIndex(x => new { x.WorkerName, x.OccurredAtUtc });
        });

        modelBuilder.Entity<TelemetryBucketRollup>(entity =>
        {
            entity.ToTable("telemetry_bucket_rollups");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantPublicId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.BucketSize).HasMaxLength(16).IsRequired();
            entity.Property(x => x.BucketStartUtc).IsRequired();
            entity.Property(x => x.ComputedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.TenantPublicId, x.BucketSize, x.BucketStartUtc }).IsUnique();
            entity.HasIndex(x => new { x.BucketSize, x.BucketStartUtc });
        });

        modelBuilder.Entity<TelemetryFailureGroupRollup>(entity =>
        {
            entity.ToTable("telemetry_failure_group_rollups");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantPublicId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.BucketSize).HasMaxLength(16).IsRequired();
            entity.Property(x => x.BucketStartUtc).IsRequired();
            entity.Property(x => x.JobName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ErrorType).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.ComputedAtUtc).IsRequired();
            entity.HasIndex(x => new
            {
                x.TenantPublicId,
                x.BucketSize,
                x.BucketStartUtc,
                x.JobName,
                x.ErrorType,
                x.ErrorMessage
            }).IsUnique();
            entity.HasIndex(x => new { x.BucketSize, x.BucketStartUtc });
        });

        modelBuilder.Entity<TelemetryRollupWatermark>(entity =>
        {
            entity.ToTable("telemetry_rollup_watermarks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantPublicId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.BucketSize).HasMaxLength(16).IsRequired();
            entity.Property(x => x.LastRolledUpBucketStartUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.TenantPublicId, x.BucketSize }).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}
