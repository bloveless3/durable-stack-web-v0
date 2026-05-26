using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DurableStack.App.Models;
using Microsoft.AspNetCore.Authorization;
using DurableStack.App.Extensions;
using DurableStack.App.Services.Identity;
using DurableStack.App.Services.Onboarding;

namespace DurableStack.App.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IOnboardingService _onboardingService;

    public HomeController(
        ICurrentUserContext currentUserContext,
        IOnboardingService onboardingService)
    {
        _currentUserContext = currentUserContext;
        _onboardingService = onboardingService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!await _onboardingService.HasOrganizationAsync(cancellationToken))
        {
            return RedirectToAction("Index", "Onboarding");
        }

        if (!await _onboardingService.HasProjectAsync(cancellationToken))
        {
            return RedirectToAction("Project", "Onboarding");
        }

        if (!await _onboardingService.HasTenantAsync(cancellationToken))
        {
            return RedirectToAction("Tenant", "Onboarding");
        }

        return View(new AppHomeViewModel
        {
            DashboardStatus = "Starting",
            LastUpdateLabel = "Waiting for first report query"
        });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Profile()
    {
        TempData.AddInfoToast($"Signed in as {_currentUserContext.Email ?? "unknown user"}.", 3000);
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
