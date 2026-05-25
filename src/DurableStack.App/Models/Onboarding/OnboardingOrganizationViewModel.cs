using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DurableStack.App.Models.Onboarding;

public sealed class OnboardingOrganizationViewModel : IValidatableObject
{
    [Display(Name = "Is this registration for a company?")]
    public bool IsCompanyRegistration { get; set; } = true;

    [Display(Name = "Company name")]
    [StringLength(200)]
    public string? OrganizationName { get; set; }

    public string PersonalDisplayName { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsCompanyRegistration)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(OrganizationName))
        {
            yield return new ValidationResult("Company name is required.", [nameof(OrganizationName)]);
        }
    }
}
