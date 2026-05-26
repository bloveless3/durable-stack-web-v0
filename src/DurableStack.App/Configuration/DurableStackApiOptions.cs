namespace DurableStack.App.Configuration;

public sealed class DurableStackApiOptions
{
    public const string SectionName = "DurableStackApi";

    public string BaseUrl { get; set; } = "https://localhost:5001";
}
