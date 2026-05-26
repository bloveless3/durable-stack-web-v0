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

    [HttpPost("dashboard-summary")]
    public async Task<IActionResult> DashboardSummary([FromBody] DashboardSummaryRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _reportsQueryService.QueryDashboardSummaryAsync(
                request?.SinceCursor,
                HttpContext.TraceIdentifier,
                cancellationToken);

            if (summary is null)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Report service is unavailable." });
            }

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load dashboard summary from BFF reports query service.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Unable to load dashboard summary." });
        }
    }
}
