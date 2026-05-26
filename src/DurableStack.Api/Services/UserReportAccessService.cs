using DurableStack.ControlPlane;
using DurableStack.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DurableStack.Api.Services;

public interface IUserReportAccessService
{
    Task<UserReportScope> BuildScopeAsync(UserAccessContext user, ReportSummaryQueryRequest? request, CancellationToken cancellationToken = default);
}

public sealed record UserReportScope(
    IReadOnlySet<Guid> OrganizationIds,
    IReadOnlySet<Guid> ProjectIds,
    IReadOnlySet<Guid> TenantIds,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    DateTimeOffset? IncrementalSinceUtc);

public sealed class UserReportAccessService : IUserReportAccessService
{
    private const int MaxFilterIds = 100;
    private static readonly TimeSpan MaxWindow = TimeSpan.FromDays(90);

    private readonly ControlPlaneDbContext _controlPlaneDbContext;

    public UserReportAccessService(ControlPlaneDbContext controlPlaneDbContext)
    {
        _controlPlaneDbContext = controlPlaneDbContext;
    }

    public async Task<UserReportScope> BuildScopeAsync(UserAccessContext user, ReportSummaryQueryRequest? request, CancellationToken cancellationToken = default)
    {
        request ??= new ReportSummaryQueryRequest();

        ValidateSize(request.OrganizationIds, nameof(request.OrganizationIds));
        ValidateSize(request.ProjectIds, nameof(request.ProjectIds));
        ValidateSize(request.TenantIds, nameof(request.TenantIds));

        var now = DateTimeOffset.UtcNow;
        var toUtc = request.ToUtc ?? now;
        var fromUtc = request.FromUtc ?? toUtc.AddDays(-7);

        DateTimeOffset? incrementalSinceUtc = null;

        if (!string.IsNullOrWhiteSpace(request.SinceCursor))
        {
            incrementalSinceUtc = ParseSinceCursor(request.SinceCursor);
            if (incrementalSinceUtc > toUtc)
            {
                throw new UserReportScopeException("invalid_since_cursor", "SinceCursor cannot be later than the query end time.");
            }

            if (incrementalSinceUtc > fromUtc)
            {
                fromUtc = incrementalSinceUtc.Value;
            }
        }

        if (fromUtc > toUtc)
        {
            throw new UserReportScopeException("invalid_time_range", "FromUtc must be less than or equal to ToUtc.");
        }

        if (toUtc - fromUtc > MaxWindow)
        {
            throw new UserReportScopeException("invalid_time_range", "Requested time window exceeds maximum supported range (90 days).");
        }

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

        return new UserReportScope(
            organizationSet,
            projectSet,
            tenantSet,
            fromUtc,
            toUtc,
            incrementalSinceUtc);
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

    private static DateTimeOffset ParseSinceCursor(string cursor)
    {
        var trimmed = cursor.Trim();

        try
        {
            var decoded = Convert.FromBase64String(trimmed);
            var decodedText = System.Text.Encoding.UTF8.GetString(decoded);

            if (!long.TryParse(decodedText, out var unixMilliseconds))
            {
                throw new UserReportScopeException("invalid_since_cursor", "SinceCursor is invalid.");
            }

            return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
        }
        catch (FormatException)
        {
            throw new UserReportScopeException("invalid_since_cursor", "SinceCursor is invalid.");
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new UserReportScopeException("invalid_since_cursor", "SinceCursor is invalid.");
        }
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
