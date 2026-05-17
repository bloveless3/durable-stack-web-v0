namespace DurableStack.App.Services.Api;

public sealed class DurableStackApiOptions
{
    public const string SectionName = "DurableStackApi";

    public string BaseUrl { get; set; } = "https://localhost:5001";

    public string TenantId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}
