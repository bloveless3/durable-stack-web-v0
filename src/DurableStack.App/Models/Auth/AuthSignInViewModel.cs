using System.ComponentModel.DataAnnotations;

namespace DurableStack.App.Models.Auth;

public sealed class AuthSignInViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    public bool AllowPasswordSignIn { get; set; }

    public List<string> ExternalProviders { get; set; } = [];

    public bool ExternalOnly => !AllowPasswordSignIn && ExternalProviders.Count > 0;
}
