using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DurableStack.App.Models;
using DurableStack.App.Services.Api;
using Microsoft.AspNetCore.Authorization;
using DurableStack.App.Extensions;

namespace DurableStack.App.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly DurableStackApiClient _apiClient;
    private readonly DurableStackApiOptions _apiOptions;

    public HomeController(
        ILogger<HomeController> logger,
        DurableStackApiClient apiClient,
        Microsoft.Extensions.Options.IOptions<DurableStackApiOptions> apiOptions)
    {
        _logger = logger;
        _apiClient = apiClient;
        _apiOptions = apiOptions.Value;
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

            TempData.AddToastNotification("success", "Here is a new success message");
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

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
