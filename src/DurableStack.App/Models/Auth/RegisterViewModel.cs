using System.ComponentModel.DataAnnotations;

namespace DurableStack.App.Models.Auth;

public sealed class RegisterViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "First name")]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Last name")]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "I agree to the Terms and Privacy Policy")]
    [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the terms to continue.")]
    public bool AcceptTerms { get; set; }
}
