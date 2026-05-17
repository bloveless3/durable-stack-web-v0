using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DurableStack.ControlPlane;

public sealed class ControlPlaneDbContextFactory : IDesignTimeDbContextFactory<ControlPlaneDbContext>
{
    public ControlPlaneDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DURABLESTACK_CONTROLPLANE_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=durablestack_control;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<ControlPlaneDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ControlPlaneDbContext(optionsBuilder.Options);
    }
}
