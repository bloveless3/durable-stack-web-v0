using System.ComponentModel.DataAnnotations;
using DurableStack.App.Validation;

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
    [StringLength(100, MinimumLength = 12, ErrorMessage = "Password must be at least 12 characters long.")]
    [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+$",
        ErrorMessage = "Password must include uppercase, lowercase, a number, and a symbol.")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "I agree to the Terms and Privacy Policy")]
    [MustBeTrue(ErrorMessage = "You must accept the terms to continue.")]
    public bool AcceptTerms { get; set; }
}
