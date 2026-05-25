namespace DurableStack.App.Data;

public sealed class UserAttribute
{
    public int Id { get; set; }

    public Guid UserId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ApplicationUser? User { get; set; }
}
