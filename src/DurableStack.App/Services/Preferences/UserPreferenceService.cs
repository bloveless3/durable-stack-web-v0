using DurableStack.App.Data;
using DurableStack.App.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace DurableStack.App.Services.Preferences;

public interface IUserPreferenceService
{
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> GetValuesAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default);
}

public sealed class UserPreferenceService : IUserPreferenceService
{
    private readonly AppIdentityDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;

    public UserPreferenceService(AppIdentityDbContext dbContext, ICurrentUserContext currentUserContext)
    {
        _dbContext = dbContext;
        _currentUserContext = currentUserContext;
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeKey(key);
        if (normalizedKey is null)
        {
            return null;
        }

        var userId = _currentUserContext.UserId;
        if (!userId.HasValue)
        {
            return null;
        }

        return await _dbContext.UserAttributes
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value && x.Key == normalizedKey)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetValuesAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var normalizedKeys = keys
            .Select(NormalizeKey)
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedKeys.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return await _dbContext.UserAttributes
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value && normalizedKeys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase, cancellationToken);
    }

    public async Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeKey(key) ?? throw new ArgumentException("Preference key is required.", nameof(key));
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var userId = _currentUserContext.UserId;
        if (!userId.HasValue)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        var existing = await _dbContext.UserAttributes
            .FirstOrDefaultAsync(x => x.UserId == userId.Value && x.Key == normalizedKey, cancellationToken);

        if (existing is null)
        {
            _dbContext.UserAttributes.Add(new UserAttribute
            {
                UserId = userId.Value,
                Key = normalizedKey,
                Value = value,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return key.Trim();
    }
}
