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
    public IActionResult SignIn(string email)
    {
        return View(new AuthSignInViewModel
        {
            Email = email
        });
    }

    [HttpPost("auth/sign-in")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignIn(AuthSignInViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "No account exists for that email.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent: true, lockoutOnFailure: false);
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
