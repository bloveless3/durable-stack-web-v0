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
            entity.HasOne(x => x.Batch)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.EventType, x.OccurredAtUtc });
            entity.HasIndex(x => x.RunId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
