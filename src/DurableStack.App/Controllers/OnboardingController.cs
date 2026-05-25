using DurableStack.App.Extensions;
using DurableStack.App.Models.Onboarding;
using DurableStack.App.Services.Identity;
using DurableStack.App.Services.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DurableStack.App.Controllers;

[Authorize]
public sealed class OnboardingController : Controller
{
    private const string CompletionModelTempDataKey = "Onboarding.CompleteModel";

    private readonly IOnboardingService _onboardingService;
    private readonly ICurrentUserContext _currentUserContext;

    public OnboardingController(IOnboardingService onboardingService, ICurrentUserContext currentUserContext)
    {
        _onboardingService = onboardingService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet("onboarding")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (await _onboardingService.HasOrganizationAsync(cancellationToken))
        {
            return RedirectToAction(nameof(Project));
        }

        ViewData["JustRegistered"] = string.Equals(TempData.Peek("Onboarding.JustRegistered") as string, "true", StringComparison.OrdinalIgnoreCase);
        return View(await CreateDefaultModelAsync(cancellationToken));
    }

    [HttpPost("onboarding")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(OnboardingOrganizationViewModel model, CancellationToken cancellationToken)
    {
        if (await _onboardingService.HasOrganizationAsync(cancellationToken))
        {
            return RedirectToAction("Index", "Home");
        }

        if (!ModelState.IsValid)
        {
            model.PersonalDisplayName = await ResolvePersonalDisplayNameAsync(cancellationToken);
            ViewData["JustRegistered"] = string.Equals(TempData.Peek("Onboarding.JustRegistered") as string, "true", StringComparison.OrdinalIgnoreCase);
            return View(model);
        }

        TempData.Remove("Onboarding.JustRegistered");

        var personalDisplayName = await ResolvePersonalDisplayNameAsync(cancellationToken);
        var organizationName = model.IsCompanyRegistration
            ? model.OrganizationName!.Trim()
            : personalDisplayName;

        await _onboardingService.CreateInitialOrganizationAsync(organizationName, cancellationToken);

        return RedirectToAction(nameof(Project));
    }

    [HttpGet("onboarding/project")]
    public async Task<IActionResult> Project(CancellationToken cancellationToken)
    {
        if (!await _onboardingService.HasOrganizationAsync(cancellationToken))
        {
            return RedirectToAction(nameof(Index));
        }

        if (await _onboardingService.HasProjectAsync(cancellationToken))
        {
            return RedirectToAction(nameof(Tenant));
        }

        return View(new OnboardingProjectViewModel());
    }

    [HttpPost("onboarding/project")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Project(OnboardingProjectViewModel model, CancellationToken cancellationToken)
    {
        if (!await _onboardingService.HasOrganizationAsync(cancellationToken))
        {
            return RedirectToAction(nameof(Index));
        }

        if (await _onboardingService.HasProjectAsync(cancellationToken))
        {
            return RedirectToAction("Index", "Home");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _onboardingService.CreateInitialProjectAsync(model.ProjectName, cancellationToken);

        return RedirectToAction(nameof(Tenant));
    }

    [HttpGet("onboarding/tenant")]
    public async Task<IActionResult> Tenant(CancellationToken cancellationToken)
    {
        if (!await _onboardingService.HasOrganizationAsync(cancellationToken))
        {
            return RedirectToAction(nameof(Index));
        }

        if (!await _onboardingService.HasProjectAsync(cancellationToken))
        {
            return RedirectToAction(nameof(Project));
        }

        if (await _onboardingService.HasTenantAsync(cancellationToken))
        {
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost("onboarding/tenant")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TenantProvision(CancellationToken cancellationToken)
    {
        if (!await _onboardingService.HasOrganizationAsync(cancellationToken))
        {
            return RedirectToAction(nameof(Index));
        }

        if (!await _onboardingService.HasProjectAsync(cancellationToken))
        {
            return RedirectToAction(nameof(Project));
        }

        if (await _onboardingService.HasTenantAsync(cancellationToken))
        {
            return RedirectToAction("Index", "Home");
        }

        var result = await _onboardingService.CreateInitialTenantAsync(cancellationToken);
        TempData[CompletionModelTempDataKey] = JsonSerializer.Serialize(new OnboardingCompleteViewModel
        {
            TenantId = result.PublicTenantId,
            ClientSecret = result.ClientSecret,
            EnvironmentName = result.EnvironmentName
        });

        return RedirectToAction(nameof(Complete));
    }

    [HttpGet("onboarding/complete")]
    public IActionResult Complete()
    {
        var raw = TempData.Peek(CompletionModelTempDataKey) as string;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return RedirectToAction("Index", "Home");
        }

        var model = JsonSerializer.Deserialize<OnboardingCompleteViewModel>(raw);
        if (model is null)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(model);
    }

    [HttpPost("onboarding/complete")]
    [ValidateAntiForgeryToken]
    public IActionResult CompleteConfirmed([FromForm] bool savedClientSecret)
    {
        var raw = TempData.Peek(CompletionModelTempDataKey) as string;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return RedirectToAction("Index", "Home");
        }

        var model = JsonSerializer.Deserialize<OnboardingCompleteViewModel>(raw);
        if (model is null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (!savedClientSecret)
        {
            ModelState.AddModelError(string.Empty, "Please confirm that you saved the client secret before continuing.");
            return View("Complete", model);
        }

        TempData.Remove(CompletionModelTempDataKey);
        TempData.AddSuccessToast("Onboarding complete. Welcome to DurableStack.");

        return RedirectToAction("Index", "Home");
    }

    private async Task<OnboardingOrganizationViewModel> CreateDefaultModelAsync(CancellationToken cancellationToken)
    {
        return new OnboardingOrganizationViewModel
        {
            IsCompanyRegistration = true,
            PersonalDisplayName = await ResolvePersonalDisplayNameAsync(cancellationToken)
        };
    }

    private async Task<string> ResolvePersonalDisplayNameAsync(CancellationToken cancellationToken)
    {
        var user = await _currentUserContext.GetUserAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(user?.DisplayName))
        {
            return user.DisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(_currentUserContext.DisplayName))
        {
            return _currentUserContext.DisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(_currentUserContext.Email))
        {
            return _currentUserContext.Email.Trim().ToLowerInvariant();
        }

        return "Personal Workspace";
    }
}
