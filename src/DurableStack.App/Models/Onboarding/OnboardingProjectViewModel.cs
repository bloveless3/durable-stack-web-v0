using System.ComponentModel.DataAnnotations;

namespace DurableStack.App.Models.Onboarding;

public sealed class OnboardingProjectViewModel
{
    [Required]
    [Display(Name = "Project name")]
    [StringLength(200)]
    public string ProjectName { get; set; } = string.Empty;
}
