using DurableStack.ControlPlane;
using DurableStack.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DurableStack.Api.Services;

public interface IUserReportAccessService
{
    Task<UserDashboardReportScope> BuildDashboardScopeAsync(UserAccessContext user, ReportDashboardQueryRequest? request, CancellationToken cancellationToken = default);
}

public sealed record UserDashboardReportScope(
    IReadOnlySet<Guid> OrganizationIds,
    IReadOnlySet<Guid> ProjectIds,
    IReadOnlySet<Guid> TenantIds,
    string Timeframe,
    string BucketSize,
    TimeSpan BucketInterval,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc);

public sealed class UserReportAccessService : IUserReportAccessService
{
    private const int MaxFilterIds = 100;

    private readonly ControlPlaneDbContext _controlPlaneDbContext;

    public UserReportAccessService(ControlPlaneDbContext controlPlaneDbContext)
    {
        _controlPlaneDbContext = controlPlaneDbContext;
    }

    public async Task<UserDashboardReportScope> BuildDashboardScopeAsync(UserAccessContext user, ReportDashboardQueryRequest? request, CancellationToken cancellationToken = default)
    {
        request ??= new ReportDashboardQueryRequest();

        ValidateSize(request.OrganizationIds, nameof(request.OrganizationIds));
        ValidateSize(request.ProjectIds, nameof(request.ProjectIds));
        ValidateSize(request.TenantIds, nameof(request.TenantIds));

        var now = DateTimeOffset.UtcNow;
        var timeframe = NormalizeTimeframe(request.Timeframe);
        var (windowSize, bucketInterval, bucketSize) = timeframe switch
        {
            "last_hour" => (TimeSpan.FromHours(1), TimeSpan.FromMinutes(1), "1m"),
            "last_24h" => (TimeSpan.FromHours(24), TimeSpan.FromMinutes(15), "15m"),
            "last_7d" => (TimeSpan.FromDays(7), TimeSpan.FromHours(2), "2h"),
            "last_30d" => (TimeSpan.FromDays(30), TimeSpan.FromHours(12), "12h"),
            _ => throw new UserReportScopeException("invalid_timeframe", "Timeframe must be one of: last_hour, last_24h, last_7d, last_30d.")
        };

        var toUtc = now;
        var fromUtc = toUtc - windowSize;

        var organizationIds = await _controlPlaneDbContext.OrganizationMembers
            .AsNoTracking()
            .Where(x => x.UserId == user.UserId)
            .Select(x => x.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var projectIds = await _controlPlaneDbContext.Projects
            .AsNoTracking()
            .Where(x => organizationIds.Contains(x.OrganizationId))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var tenantIds = await _controlPlaneDbContext.Tenants
            .AsNoTracking()
            .Where(x => projectIds.Contains(x.ProjectId))
            .Select(x => new { x.Id, x.ProjectId })
            .ToListAsync(cancellationToken);

        var organizationSet = ApplyRequestedFilter(organizationIds, request.OrganizationIds);
        var projectSet = ApplyRequestedFilter(projectIds, request.ProjectIds);

        var projectIdsInOrganizations = await _controlPlaneDbContext.Projects
            .AsNoTracking()
            .Where(x => organizationSet.Contains(x.OrganizationId))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        projectSet.IntersectWith(projectIdsInOrganizations);

        var tenantSet = ApplyRequestedFilter(tenantIds.Select(x => x.Id), request.TenantIds);

        var tenantIdsInProjects = tenantIds
            .Where(x => projectSet.Contains(x.ProjectId))
            .Select(x => x.Id);

        tenantSet.IntersectWith(tenantIdsInProjects);

        return new UserDashboardReportScope(
            organizationSet,
            projectSet,
            tenantSet,
            timeframe,
            bucketSize,
            bucketInterval,
            fromUtc,
            toUtc);
    }

    private static HashSet<Guid> ApplyRequestedFilter(IEnumerable<Guid> allowed, List<Guid> requested)
    {
        var allowedSet = new HashSet<Guid>(allowed);
        if (requested.Count == 0)
        {
            return allowedSet;
        }

        var filtered = requested.Where(allowedSet.Contains);
        return new HashSet<Guid>(filtered);
    }

    private static void ValidateSize(List<Guid> ids, string propertyName)
    {
        if (ids.Count > MaxFilterIds)
        {
            throw new UserReportScopeException(
                "filter_limit_exceeded",
                $"{propertyName} cannot contain more than {MaxFilterIds} values.");
        }
    }

    private static string NormalizeTimeframe(string? timeframe)
    {
        if (string.IsNullOrWhiteSpace(timeframe))
        {
            return "last_24h";
        }

        return timeframe.Trim().ToLowerInvariant();
    }
}

public sealed class UserReportScopeException : Exception
{
    public UserReportScopeException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
