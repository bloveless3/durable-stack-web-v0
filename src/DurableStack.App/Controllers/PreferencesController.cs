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
    private readonly ILogger<PreferencesController> _logger;

    public PreferencesController(IUserPreferenceService userPreferenceService, ILogger<PreferencesController> logger)
    {
        _userPreferenceService = userPreferenceService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string[] key, CancellationToken cancellationToken)
    {
        var normalizedKeys = key
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedKeys.Length == 0)
        {
            return BadRequest(new { error = "At least one key query parameter is required." });
        }

        var unsupportedKeys = normalizedKeys
            .Where(x => !PreferenceKeys.AllowedKeys.Contains(x))
            .ToArray();

        if (unsupportedKeys.Length > 0)
        {
            _logger.LogWarning("Unsupported preference keys requested: {Keys}", string.Join(", ", unsupportedKeys));
            return BadRequest(new { error = $"Unsupported preference key(s): {string.Join(", ", unsupportedKeys)}" });
        }

        var values = await _userPreferenceService.GetValuesAsync(normalizedKeys, cancellationToken);
        return Ok(values);
    }

    [HttpPost]
    public async Task<IActionResult> Set([FromBody] PreferenceUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedKey = request.Key.Trim();
        if (!PreferenceKeys.AllowedKeys.Contains(normalizedKey))
        {
            _logger.LogWarning("Unsupported preference key attempted: {Key}", normalizedKey);
            return BadRequest(new { error = $"Unsupported preference key: {normalizedKey}" });
        }

        if (request.Value.Length > 200)
        {
            return BadRequest(new { error = "Preference value exceeds maximum length (200)." });
        }

        var normalizedValue = request.Value.Trim();

        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return BadRequest(new { error = "Preference value is required." });
        }

        await _userPreferenceService.SetValueAsync(normalizedKey, normalizedValue, cancellationToken);
        return Ok(new { saved = true });
    }
}
