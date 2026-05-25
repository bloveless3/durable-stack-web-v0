using DurableStack.App.Models.Preferences;
using DurableStack.App.Services.Preferences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DurableStack.App.Controllers;

[Authorize]
[ApiController]
[Route("api/preferences")]
public sealed class PreferencesController : ControllerBase
{
    private readonly IUserPreferenceService _userPreferenceService;

    public PreferencesController(IUserPreferenceService userPreferenceService)
    {
        _userPreferenceService = userPreferenceService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string[] key, CancellationToken cancellationToken)
    {
        if (key.Length == 0)
        {
            return BadRequest(new { error = "At least one key query parameter is required." });
        }

        var values = await _userPreferenceService.GetValuesAsync(key, cancellationToken);
        return Ok(values);
    }

    [HttpPost]
    public async Task<IActionResult> Set([FromBody] PreferenceUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        await _userPreferenceService.SetValueAsync(request.Key, request.Value, cancellationToken);
        return Ok(new { saved = true });
    }
}
