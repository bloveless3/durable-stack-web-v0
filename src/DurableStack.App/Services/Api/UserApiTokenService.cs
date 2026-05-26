using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DurableStack.App.Configuration;
using DurableStack.App.Services.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DurableStack.App.Services.Api;

public interface IUserApiTokenService
{
    Task<string> CreateReportsReadTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class UserApiTokenService : IUserApiTokenService
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IOptions<UserApiTokenOptions> _tokenOptions;

    public UserApiTokenService(ICurrentUserContext currentUserContext, IOptions<UserApiTokenOptions> tokenOptions)
    {
        _currentUserContext = currentUserContext;
        _tokenOptions = tokenOptions;
    }

    public async Task<string> CreateReportsReadTokenAsync(CancellationToken cancellationToken = default)
    {
        var user = await _currentUserContext.GetUserAsync(cancellationToken)
            ?? throw new InvalidOperationException("Authenticated user is required.");

        var options = _tokenOptions.Value;
        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString("D")),
            new(ClaimTypes.NameIdentifier, user.Id.ToString("D")),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("scope", "reports.read")
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(options.LifetimeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
