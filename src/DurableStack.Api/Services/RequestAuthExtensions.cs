using DurableStack.ControlPlane;
using Microsoft.EntityFrameworkCore;

namespace DurableStack.Api.Services;

public enum RequestAuthFailureReason
{
    MissingHeaders,
    InvalidHeaders,
    InvalidCredentials
}

public readonly record struct RequestAuthResult(
    bool Success,
    string? TenantId,
    RequestAuthFailureReason? FailureReason)
{
    public static RequestAuthResult Authenticated(string tenantId) =>
        new(true, tenantId, null);

    public static RequestAuthResult Failed(RequestAuthFailureReason reason) =>
        new(false, null, reason);
}

public static class RequestAuthExtensions
{
    private const string TenantHeader = "X-DurableStack-TenantId";
    private const string SecretHeader = "X-DurableStack-ClientSecret";

    public static async Task<RequestAuthResult> TryAuthenticateAsync(
        this HttpRequest request,
        ControlPlaneDbContext controlPlaneDb,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValue(TenantHeader, out var tenantValues) ||
            !request.Headers.TryGetValue(SecretHeader, out var secretValues))
        {
            return RequestAuthResult.Failed(RequestAuthFailureReason.MissingHeaders);
        }

        var tenantId = tenantValues.ToString().Trim();
        var secret = secretValues.ToString();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(secret))
        {
            return RequestAuthResult.Failed(RequestAuthFailureReason.InvalidHeaders);
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
                return RequestAuthResult.Authenticated(tenantId);
            }
        }

        return RequestAuthResult.Failed(RequestAuthFailureReason.InvalidCredentials);
    }
}
