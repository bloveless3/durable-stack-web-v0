using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DurableStack.ControlPlane.DependencyInjection;

public static class ControlPlaneServiceCollectionExtensions
{
    public static IServiceCollection AddControlPlanePostgres(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionName = "ControlPlane")
    {
        var connectionString = configuration.GetConnectionString(connectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Missing connection string '{connectionName}'. Configure ConnectionStrings:{connectionName}.");
        }

        services.AddDbContext<ControlPlaneDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}
