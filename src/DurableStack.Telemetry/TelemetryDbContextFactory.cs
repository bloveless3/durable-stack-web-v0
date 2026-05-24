using DurableStack.Platform.Contracts.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DurableStack.Telemetry;

public sealed class TelemetryDbContextFactory : IDesignTimeDbContextFactory<TelemetryDbContext>
{
    public TelemetryDbContext CreateDbContext(string[] args)
    {
        var connectionString = ConnectionStringResolver.Resolve(
            connectionName: "Telemetry",
            environmentVariableName: "DURABLESTACK_TELEMETRY_CONNECTION",
            preferredProjectPaths: ["src/DurableStack.Api"]);

        var optionsBuilder = new DbContextOptionsBuilder<TelemetryDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new TelemetryDbContext(optionsBuilder.Options);
    }
}
