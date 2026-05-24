using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DurableStack.App.Models;
using DurableStack.App.Services.Api;
using Microsoft.AspNetCore.Authorization;
using DurableStack.App.Extensions;
using DurableStack.App.Services.Identity;

namespace DurableStack.App.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly DurableStackApiClient _apiClient;
    private readonly DurableStackApiOptions _apiOptions;
    private readonly ICurrentUserContext _currentUserContext;

    public HomeController(
        ILogger<HomeController> logger,
        DurableStackApiClient apiClient,
        Microsoft.Extensions.Options.IOptions<DurableStackApiOptions> apiOptions,
        ICurrentUserContext currentUserContext)
    {
        _logger = logger;
        _apiClient = apiClient;
        _apiOptions = apiOptions.Value;
        _currentUserContext = currentUserContext;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new AppHomeViewModel
        {
            TenantId = string.IsNullOrWhiteSpace(_apiOptions.TenantId)
                ? null
                : _apiOptions.TenantId
        };

        try
        {
            var report = await _apiClient.GetReportSummaryAsync(cancellationToken);
            if (report is null)
            {
                model.ApiStatus = "Unavailable or unauthorized";
                return View(model);
            }

            model.ApiStatus = "Connected";
            model.TenantId = report.TenantId;
            model.TotalEvents = report.TotalEvents;
            model.FailedEvents = report.FailedEvents;
            model.LastEventAtUtc = report.LastEventAtUtc;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load report summary from API.");
            model.ApiStatus = "Unavailable";
        }

        return View(model);
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
