using Microsoft.Extensions.Configuration;

namespace DurableStack.Platform.Contracts.Configuration;

public static class ConnectionStringResolver
{
    public static string Resolve(
        string connectionName,
        string environmentVariableName,
        params string[] preferredProjectPaths)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";

        foreach (var basePath in GetCandidateBasePaths(preferredProjectPaths))
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .Build();

            var connectionString = configuration.GetConnectionString(connectionName);
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }
        }

        var fromEnvironment = Environment.GetEnvironmentVariable(environmentVariableName);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        throw new InvalidOperationException(
            $"Connection string '{connectionName}' was not found in appsettings files or env var '{environmentVariableName}'.");
    }

    private static IEnumerable<string> GetCandidateBasePaths(IEnumerable<string> preferredProjectPaths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

        for (var cursor = currentDirectory; cursor is not null; cursor = cursor.Parent)
        {
            TryAddPath(cursor.FullName);

            foreach (var relativePath in preferredProjectPaths)
            {
                var combinedPath = Path.GetFullPath(Path.Combine(cursor.FullName, relativePath));
                TryAddPath(combinedPath);
            }
        }

        return seen;

        void TryAddPath(string path)
        {
            if (Directory.Exists(path))
            {
                seen.Add(path);
            }
        }
    }
}
