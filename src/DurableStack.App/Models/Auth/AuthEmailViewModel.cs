using System.ComponentModel.DataAnnotations;

namespace DurableStack.App.Models.Auth;

public sealed class AuthEmailViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;
}
