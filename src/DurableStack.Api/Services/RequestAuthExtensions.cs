using DurableStack.ControlPlane;
using Microsoft.EntityFrameworkCore;

namespace DurableStack.Api.Services;

public static class RequestAuthExtensions
{
    private const string TenantHeader = "X-DurableStack-TenantId";
    private const string SecretHeader = "X-DurableStack-ClientSecret";

    public static async Task<(bool Success, string? TenantId)> TryAuthenticateAsync(
        this HttpRequest request,
        ControlPlaneDbContext controlPlaneDb,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValue(TenantHeader, out var tenantValues) ||
            !request.Headers.TryGetValue(SecretHeader, out var secretValues))
        {
            return (false, null);
        }

        var tenantId = tenantValues.ToString().Trim();
        var secret = secretValues.ToString();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(secret))
        {
            return (false, null);
        }

        var credentialHashes = await controlPlaneDb.Tenants
            .AsNoTracking()
            .Where(x => x.PublicTenantId == tenantId)
            .SelectMany(x => x.Credentials.Where(c => c.RevokedAtUtc == null).Select(c => c.ClientSecretHash))
            .ToListAsync(cancellationToken);

        foreach (var hash in credentialHashes)
        {
            if (CredentialHasher.Verify(secret, hash))
            {
                return (true, tenantId);
            }
        }

        return (false, null);
    }
}
