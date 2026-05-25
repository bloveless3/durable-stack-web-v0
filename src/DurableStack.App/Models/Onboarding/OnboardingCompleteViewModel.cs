namespace DurableStack.App.Models.Onboarding;

public sealed class OnboardingCompleteViewModel
{
    public string TenantId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string EnvironmentName { get; init; } = string.Empty;
}
