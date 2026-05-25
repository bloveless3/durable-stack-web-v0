using System.ComponentModel.DataAnnotations;

namespace DurableStack.App.Models.Preferences;

public sealed class PreferenceUpdateRequest
{
    [Required]
    [StringLength(200)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;
}
