using System.Security.Claims;
using DurableStack.App.Data;
using Microsoft.AspNetCore.Identity;

namespace DurableStack.App.Services.Identity;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? Email { get; }
    string? DisplayName { get; }
    Task<ApplicationUser?> GetUserAsync(CancellationToken cancellationToken = default);
}

public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor, UserManager<ApplicationUser> userManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var raw = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);

    public string? DisplayName => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);

    public async Task<ApplicationUser?> GetUserAsync(CancellationToken cancellationToken = default)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal is null || !IsAuthenticated)
        {
            return null;
        }

        return await _userManager.GetUserAsync(principal);
    }
}
