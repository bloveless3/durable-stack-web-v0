using DurableStack.App.Models.Layout;
using DurableStack.App.Services.Identity;
using DurableStack.ControlPlane;
using Microsoft.EntityFrameworkCore;

namespace DurableStack.App.Services.Preferences;

public interface IGlobalFilterOptionsProvider
{
    Task<IReadOnlyList<GlobalFilterOption>> GetOrganizationOptionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GlobalFilterOption>> GetProjectOptionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GlobalFilterOption>> GetTenantOptionsAsync(CancellationToken cancellationToken = default);
}

public sealed class GlobalFilterOptionsProvider : IGlobalFilterOptionsProvider
{
    private static readonly GlobalFilterOption AllOrganizationsOption = new()
    {
        Value = "all-organizations",
        Label = "All organizations"
    };

    private static readonly GlobalFilterOption AllProjectsOption = new()
    {
        Value = "all-projects",
        Label = "All projects"
    };

    private static readonly GlobalFilterOption AllTenantsOption = new()
    {
        Value = "all-tenants",
        Label = "All tenants"
    };

    private readonly ControlPlaneDbContext _controlPlaneDbContext;
    private readonly ICurrentUserContext _currentUserContext;

    public GlobalFilterOptionsProvider(ControlPlaneDbContext controlPlaneDbContext, ICurrentUserContext currentUserContext)
    {
        _controlPlaneDbContext = controlPlaneDbContext;
        _currentUserContext = currentUserContext;
    }

    public async Task<IReadOnlyList<GlobalFilterOption>> GetOrganizationOptionsAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue)
        {
            return [AllOrganizationsOption];
        }

        var organizations = await _controlPlaneDbContext.OrganizationMembers
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .OrderBy(x => x.Organization!.Name)
            .Select(x => new GlobalFilterOption
            {
                Value = x.OrganizationId.ToString("D"),
                Label = x.Organization!.Name
            })
            .ToListAsync(cancellationToken);

        organizations.Insert(0, AllOrganizationsOption);
        return organizations;
    }

    public async Task<IReadOnlyList<GlobalFilterOption>> GetProjectOptionsAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue)
        {
            return [AllProjectsOption];
        }

        var projects = await _controlPlaneDbContext.Projects
            .AsNoTracking()
            .Where(p => p.Organization != null && p.Organization.Members.Any(m => m.UserId == userId.Value))
            .OrderBy(p => p.Name)
            .Select(p => new GlobalFilterOption
            {
                Value = p.Id.ToString("D"),
                Label = p.Name
            })
            .ToListAsync(cancellationToken);

        projects.Insert(0, AllProjectsOption);
        return projects;
    }

    public async Task<IReadOnlyList<GlobalFilterOption>> GetTenantOptionsAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue)
        {
            return [AllTenantsOption];
        }

        var tenants = await _controlPlaneDbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Project != null && t.Project.Organization != null &&
                        t.Project.Organization.Members.Any(m => m.UserId == userId.Value))
            .OrderBy(t => t.Project!.Name)
            .ThenBy(t => t.EnvironmentName)
            .Select(t => new GlobalFilterOption
            {
                Value = t.Id.ToString("D"),
                Label = $"{t.Project!.Name} - {t.EnvironmentName}"
            })
            .ToListAsync(cancellationToken);

        tenants.Insert(0, AllTenantsOption);
        return tenants;
    }
}
