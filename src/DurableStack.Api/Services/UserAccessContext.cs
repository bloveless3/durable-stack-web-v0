using System.Security.Claims;

namespace DurableStack.Api.Services;

public sealed record UserAccessContext(Guid UserId, string? Email)
{
    public static bool TryCreate(ClaimsPrincipal principal, out UserAccessContext? context)
    {
        context = null;

        var rawUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        if (!Guid.TryParse(rawUserId, out var userId))
        {
            return false;
        }

        context = new UserAccessContext(
            userId,
            principal.FindFirstValue(ClaimTypes.Email));

        return true;
    }
}
