using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DurableStack.Telemetry.DependencyInjection;

public static class TelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddTelemetryPostgres(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionName = "Telemetry")
    {
        var connectionString = configuration.GetConnectionString(connectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Missing connection string '{connectionName}'. Configure ConnectionStrings:{connectionName}.");
        }

        services.AddDbContext<TelemetryDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}
