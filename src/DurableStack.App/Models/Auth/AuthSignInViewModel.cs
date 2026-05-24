using System.ComponentModel.DataAnnotations;

namespace DurableStack.App.Models.Auth;

public sealed class AuthSignInViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;
}
