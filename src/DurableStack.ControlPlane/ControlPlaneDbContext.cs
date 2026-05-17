using DurableStack.ControlPlane.Entities;
using Microsoft.EntityFrameworkCore;

namespace DurableStack.ControlPlane;

public sealed class ControlPlaneDbContext : DbContext
{
    public ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<User> Users => Set<User>();

    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantCredential> TenantCredentials => Set<TenantCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.Name);
            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<OrganizationMember>(entity =>
        {
            entity.ToTable("organization_members");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasMaxLength(50).IsRequired();
            entity.Property(x => x.JoinedAtUtc).IsRequired();
            entity.HasOne(x => x.Organization)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany(x => x.OrganizationMemberships)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.Organization)
                .WithMany(x => x.Projects)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EnvironmentName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PublicTenantId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.SyncEnabled).IsRequired();
            entity.Property(x => x.MaxBatchSize).IsRequired();
            entity.Property(x => x.RecommendedBatchIntervalSeconds).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.Project)
                .WithMany(x => x.Tenants)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.PublicTenantId).IsUnique();
            entity.HasIndex(x => new { x.ProjectId, x.EnvironmentName }).IsUnique();
        });

        modelBuilder.Entity<TenantCredential>(entity =>
        {
            entity.ToTable("tenant_credentials");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ClientSecretHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CredentialName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.Credentials)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.TenantId, x.RevokedAtUtc });
            entity.HasIndex(x => new { x.TenantId, x.CredentialName }).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}
