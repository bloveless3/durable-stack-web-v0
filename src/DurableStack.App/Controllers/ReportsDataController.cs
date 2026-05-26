using DurableStack.App.Models.Reports;
using DurableStack.App.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DurableStack.App.Controllers;

[Authorize]
[ApiController]
[Route("api/reports")]
public sealed class ReportsDataController : ControllerBase
{
    private readonly IReportsQueryService _reportsQueryService;
    private readonly ILogger<ReportsDataController> _logger;

    public ReportsDataController(
        IReportsQueryService reportsQueryService,
        ILogger<ReportsDataController> logger)
    {
        _reportsQueryService = reportsQueryService;
        _logger = logger;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        try
        {
            var dashboard = await _reportsQueryService.QueryDashboardAsync(
                HttpContext.TraceIdentifier,
                cancellationToken);

            if (dashboard is null)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Dashboard service is unavailable." });
            }

            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load dashboard data from BFF reports query service.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Unable to load dashboard data." });
        }
    }
}
