using DurableStack.App.Data;
using DurableStack.App.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DurableStack.App.Controllers;

public sealed class AuthController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet("auth")]
    [AllowAnonymous]
    public IActionResult Index(string? email = null)
    {
        return View(new AuthEmailViewModel
        {
            Email = email ?? string.Empty
        });
    }

    [HttpPost("auth")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AuthEmailViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim();
        var existing = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existing is null)
        {
            return RedirectToAction(nameof(Register), new { email = normalizedEmail });
        }

        return RedirectToAction(nameof(SignIn), new { email = normalizedEmail });
    }

    [HttpGet("auth/sign-in")]
    [AllowAnonymous]
    public async Task<IActionResult> SignIn(string email)
    {
        var normalizedEmail = email.Trim();
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser is null)
        {
            return RedirectToAction(nameof(Register), new { email = normalizedEmail });
        }

        var loginProviders = await _userManager.GetLoginsAsync(existingUser);
        var allowPasswordSignIn = !string.IsNullOrWhiteSpace(existingUser.PasswordHash);

        return View(new AuthSignInViewModel
        {
            Email = normalizedEmail,
            AllowPasswordSignIn = allowPasswordSignIn,
            ExternalProviders = loginProviders
                .Select(x => x.LoginProvider)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList()
        });
    }

    [HttpPost("auth/sign-in")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignIn(AuthSignInViewModel model)
    {
        var normalizedEmail = model.Email.Trim();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "No account exists for that email.");
            return View(model);
        }

        var logins = await _userManager.GetLoginsAsync(user);
        model.AllowPasswordSignIn = !string.IsNullOrWhiteSpace(user.PasswordHash);
        model.ExternalProviders = logins
            .Select(x => x.LoginProvider)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!model.AllowPasswordSignIn)
        {
            ModelState.AddModelError(string.Empty, "This account is configured for external sign-in.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Your account is temporarily locked after too many failed attempts. Try again later.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            return View(model);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet("register")]
    [AllowAnonymous]
    public IActionResult Register(string email)
    {
        return View(new RegisterViewModel
        {
            Email = email
        });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return RedirectToAction(nameof(SignIn), new { email });
        }

        var appUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = $"{model.FirstName} {model.LastName}".Trim(),
            EmailConfirmed = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(appUser, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _signInManager.SignInAsync(appUser, isPersistent: true);

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost("auth/sign-out")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOutUser()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Index));
    }

}
