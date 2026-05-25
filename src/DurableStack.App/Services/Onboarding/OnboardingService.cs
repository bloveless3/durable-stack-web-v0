using DurableStack.App.Services.Identity;
using DurableStack.App.Services.Preferences;
using DurableStack.ControlPlane;
using DurableStack.ControlPlane.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace DurableStack.App.Services.Onboarding;

public interface IOnboardingService
{
    Task<bool> HasOrganizationAsync(CancellationToken cancellationToken = default);

    Task<bool> HasProjectAsync(CancellationToken cancellationToken = default);

    Task<Guid> CreateInitialOrganizationAsync(string organizationName, CancellationToken cancellationToken = default);

    Task<Guid> CreateInitialProjectAsync(string projectName, CancellationToken cancellationToken = default);

    Task<bool> HasTenantAsync(CancellationToken cancellationToken = default);

    Task<ProvisionedTenantResult> CreateInitialTenantAsync(CancellationToken cancellationToken = default);
}

public sealed record ProvisionedTenantResult(string PublicTenantId, string ClientSecret, string EnvironmentName);

public sealed class OnboardingService : IOnboardingService
{
    private readonly ControlPlaneDbContext _controlPlaneDbContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserPreferenceService _userPreferenceService;

    public OnboardingService(
        ControlPlaneDbContext controlPlaneDbContext,
        ICurrentUserContext currentUserContext,
        IUserPreferenceService userPreferenceService)
    {
        _controlPlaneDbContext = controlPlaneDbContext;
        _currentUserContext = currentUserContext;
        _userPreferenceService = userPreferenceService;
    }

    public async Task<bool> HasOrganizationAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue)
        {
            return false;
        }

        return await _controlPlaneDbContext.OrganizationMembers
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId.Value, cancellationToken);
    }

    public async Task<Guid> CreateInitialOrganizationAsync(string organizationName, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        var user = await EnsureControlPlaneUserAsync(userId.Value, cancellationToken);

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = organizationName.Trim(),
            CreatedByUserId = user.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var membership = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            UserId = user.Id,
            Role = "Owner",
            JoinedAtUtc = DateTimeOffset.UtcNow
        };

        _controlPlaneDbContext.Organizations.Add(organization);
        _controlPlaneDbContext.OrganizationMembers.Add(membership);

        await _controlPlaneDbContext.SaveChangesAsync(cancellationToken);

        await _userPreferenceService.SetValueAsync(
            PreferenceKeys.GlobalFilterOrganization,
            organization.Id.ToString("D"),
            cancellationToken);

        return organization.Id;
    }

    public async Task<bool> HasProjectAsync(CancellationToken cancellationToken = default)
    {
        var organizationId = await ResolveOnboardingOrganizationIdAsync(cancellationToken);
        if (!organizationId.HasValue)
        {
            return false;
        }

        return await _controlPlaneDbContext.Projects
            .AsNoTracking()
            .AnyAsync(x => x.OrganizationId == organizationId.Value, cancellationToken);
    }

    public async Task<Guid> CreateInitialProjectAsync(string projectName, CancellationToken cancellationToken = default)
    {
        var organizationId = await ResolveOnboardingOrganizationIdAsync(cancellationToken);
        if (!organizationId.HasValue)
        {
            throw new InvalidOperationException("Organization setup is required before creating a project.");
        }

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId.Value,
            Name = projectName.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _controlPlaneDbContext.Projects.Add(project);
        await _controlPlaneDbContext.SaveChangesAsync(cancellationToken);

        await _userPreferenceService.SetValueAsync(
            PreferenceKeys.GlobalFilterProject,
            project.Id.ToString("D"),
            cancellationToken);

        return project.Id;
    }

    public async Task<bool> HasTenantAsync(CancellationToken cancellationToken = default)
    {
        var projectId = await ResolveOnboardingProjectIdAsync(cancellationToken);
        if (!projectId.HasValue)
        {
            return false;
        }

        return await _controlPlaneDbContext.Tenants
            .AsNoTracking()
            .AnyAsync(x => x.ProjectId == projectId.Value, cancellationToken);
    }

    public async Task<ProvisionedTenantResult> CreateInitialTenantAsync(CancellationToken cancellationToken = default)
    {
        var projectId = await ResolveOnboardingProjectIdAsync(cancellationToken);
        if (!projectId.HasValue)
        {
            throw new InvalidOperationException("Project setup is required before provisioning a tenant.");
        }

        const string environmentName = "Development";

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId.Value,
            EnvironmentName = environmentName,
            PublicTenantId = $"tenant_{Guid.NewGuid():N}",
            SyncEnabled = true,
            DetailedErrorSyncEnabled = false,
            MaxBatchSize = 1000,
            RecommendedBatchIntervalSeconds = 15,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var rawSecret = GenerateClientSecret();
        var credential = new TenantCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            CredentialName = "default",
            ClientSecretHash = HashClientSecret(rawSecret),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _controlPlaneDbContext.Tenants.Add(tenant);
        _controlPlaneDbContext.TenantCredentials.Add(credential);
        await _controlPlaneDbContext.SaveChangesAsync(cancellationToken);

        await _userPreferenceService.SetValueAsync(
            PreferenceKeys.GlobalFilterEnvironment,
            "dev",
            cancellationToken);

        return new ProvisionedTenantResult(tenant.PublicTenantId, rawSecret, tenant.EnvironmentName);
    }

    private async Task<User> EnsureControlPlaneUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var identityUser = await _currentUserContext.GetUserAsync(cancellationToken);
        var resolvedEmail = identityUser?.Email?.Trim().ToLowerInvariant()
            ?? _currentUserContext.Email?.Trim().ToLowerInvariant();
        var resolvedDisplayName = identityUser?.DisplayName?.Trim();

        if (string.IsNullOrWhiteSpace(resolvedDisplayName))
        {
            resolvedDisplayName = _currentUserContext.DisplayName?.Trim();
        }

        if (string.IsNullOrWhiteSpace(resolvedDisplayName))
        {
            resolvedDisplayName = resolvedEmail;
        }

        var existing = await _controlPlaneDbContext.Users
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (existing is not null)
        {
            if (!string.IsNullOrWhiteSpace(resolvedEmail) &&
                !string.Equals(existing.Email, resolvedEmail, StringComparison.OrdinalIgnoreCase))
            {
                existing.Email = resolvedEmail;
            }

            if (!string.IsNullOrWhiteSpace(resolvedDisplayName) &&
                !string.Equals(existing.DisplayName, resolvedDisplayName, StringComparison.Ordinal))
            {
                existing.DisplayName = resolvedDisplayName;
            }

            return existing;
        }

        if (string.IsNullOrWhiteSpace(resolvedEmail))
        {
            throw new InvalidOperationException("Authenticated user email is required.");
        }

        if (string.IsNullOrWhiteSpace(resolvedDisplayName))
        {
            throw new InvalidOperationException("Authenticated user display name is required.");
        }

        var user = new User
        {
            Id = userId,
            Email = resolvedEmail,
            DisplayName = resolvedDisplayName,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _controlPlaneDbContext.Users.Add(user);
        return user;
    }

    private async Task<Guid?> ResolveOnboardingOrganizationIdAsync(CancellationToken cancellationToken)
    {
        var preferredOrganizationId = await _userPreferenceService
            .GetValueAsync(PreferenceKeys.GlobalFilterOrganization, cancellationToken);

        if (Guid.TryParse(preferredOrganizationId, out var parsedOrganizationId))
        {
            return parsedOrganizationId;
        }

        var userId = _currentUserContext.UserId;
        if (!userId.HasValue)
        {
            return null;
        }

        return await _controlPlaneDbContext.OrganizationMembers
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .OrderBy(x => x.JoinedAtUtc)
            .Select(x => (Guid?)x.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveOnboardingProjectIdAsync(CancellationToken cancellationToken)
    {
        var preferredProjectId = await _userPreferenceService
            .GetValueAsync(PreferenceKeys.GlobalFilterProject, cancellationToken);

        if (Guid.TryParse(preferredProjectId, out var parsedProjectId))
        {
            return parsedProjectId;
        }

        var organizationId = await ResolveOnboardingOrganizationIdAsync(cancellationToken);
        if (!organizationId.HasValue)
        {
            return null;
        }

        return await _controlPlaneDbContext.Projects
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId.Value)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string GenerateClientSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string HashClientSecret(string secret)
    {
        const int saltSize = 16;
        const int keySize = 32;
        const int iterations = 100_000;

        var salt = RandomNumberGenerator.GetBytes(saltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, HashAlgorithmName.SHA256, keySize);
        return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }
}
