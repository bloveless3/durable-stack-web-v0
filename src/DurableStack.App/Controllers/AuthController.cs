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
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpGet("auth")]
    [AllowAnonymous]
    public IActionResult Index(string? email = null, string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocalOrHome(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;

        return View(new AuthEmailViewModel
        {
            Email = email ?? string.Empty
        });
    }

    [HttpPost("auth")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AuthEmailViewModel model, string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocalOrHome(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var existing = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existing is null)
        {
            return RedirectToAction(nameof(Register), new { email = normalizedEmail, returnUrl });
        }

        return RedirectToAction(nameof(SignIn), new { email = normalizedEmail, returnUrl });
    }

    [HttpGet("auth/sign-in")]
    [AllowAnonymous]
    public async Task<IActionResult> SignIn(string email, string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocalOrHome(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;

        if (string.IsNullOrWhiteSpace(email))
        {
            return RedirectToAction(nameof(Index), new { returnUrl });
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser is null)
        {
            return RedirectToAction(nameof(Register), new { email = normalizedEmail, returnUrl });
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
    public async Task<IActionResult> SignIn(AuthSignInViewModel model, string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocalOrHome(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
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

        if (!model.AllowPasswordSignIn)
        {
            ModelState.AddModelError(string.Empty, "This account is configured for external sign-in.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account locked out for email {Email}.", normalizedEmail);
            ModelState.AddModelError(string.Empty, "Your account is temporarily locked after too many failed attempts. Try again later.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            return View(model);
        }

        return RedirectToLocalOrHome(returnUrl);
    }

    [HttpGet("register")]
    [AllowAnonymous]
    public IActionResult Register(string email, string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocalOrHome(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;

        return View(new RegisterViewModel
        {
            Email = email
        });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocalOrHome(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim().ToLowerInvariant();
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return RedirectToAction(nameof(SignIn), new { email, returnUrl });
        }

        var appUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = $"{model.FirstName.Trim()} {model.LastName.Trim()}".Trim(),
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

        var addViewerRole = await _userManager.AddToRoleAsync(appUser, DurableStack.App.Menu.AppRoles.Viewer);
        if (!addViewerRole.Succeeded)
        {
            foreach (var error in addViewerRole.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await _userManager.DeleteAsync(appUser);
            return View(model);
        }

        await _signInManager.SignInAsync(appUser, isPersistent: true);

        return RedirectToLocalOrHome(returnUrl);
    }

    [Authorize]
    [HttpPost("auth/sign-out")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOutUser()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Index));
    }

    private IActionResult RedirectToLocalOrHome(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

}
