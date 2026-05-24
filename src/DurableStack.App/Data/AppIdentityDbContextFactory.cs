using DurableStack.Platform.Contracts.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DurableStack.App.Data;

public sealed class AppIdentityDbContextFactory : IDesignTimeDbContextFactory<AppIdentityDbContext>
{
    public AppIdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = ConnectionStringResolver.Resolve(
            connectionName: "ControlPlane",
            environmentVariableName: "DURABLESTACK_CONTROLPLANE_CONNECTION",
            preferredProjectPaths: [
                "src/DurableStack.App",
                "src/DurableStack.Api"
            ]);

        var optionsBuilder = new DbContextOptionsBuilder<AppIdentityDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppIdentityDbContext(optionsBuilder.Options);
    }
}
