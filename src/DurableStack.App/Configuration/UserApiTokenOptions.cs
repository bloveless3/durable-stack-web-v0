namespace DurableStack.App.Configuration;

public sealed class UserApiTokenOptions
{
    public string Issuer { get; set; } = "DurableStack.App";

    public string Audience { get; set; } = "DurableStack.Api";

    public string SigningKey { get; set; } = "dev-only-signing-key-change-me-please-32chars";

    public int LifetimeMinutes { get; set; } = 10;
}
