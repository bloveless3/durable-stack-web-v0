using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DurableStack.App.Data;

public sealed class AppIdentityDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.UserName).HasMaxLength(320);
            entity.Property(x => x.NormalizedEmail).HasMaxLength(320);
            entity.Property(x => x.NormalizedUserName).HasMaxLength(320);
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
        });

        builder.Entity<IdentityRole<Guid>>(entity => entity.ToTable("app_roles"));
        builder.Entity<IdentityUserRole<Guid>>(entity => entity.ToTable("app_user_roles"));
        builder.Entity<IdentityUserClaim<Guid>>(entity => entity.ToTable("app_user_claims"));
        builder.Entity<IdentityUserLogin<Guid>>(entity => entity.ToTable("app_user_logins"));
        builder.Entity<IdentityUserToken<Guid>>(entity => entity.ToTable("app_user_tokens"));
        builder.Entity<IdentityRoleClaim<Guid>>(entity => entity.ToTable("app_role_claims"));
    }
}
