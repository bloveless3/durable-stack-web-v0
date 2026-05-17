using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DurableStack.Telemetry;

public sealed class TelemetryDbContextFactory : IDesignTimeDbContextFactory<TelemetryDbContext>
{
    public TelemetryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DURABLESTACK_TELEMETRY_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=durablestack_telemetry;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<TelemetryDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new TelemetryDbContext(optionsBuilder.Options);
    }
}
