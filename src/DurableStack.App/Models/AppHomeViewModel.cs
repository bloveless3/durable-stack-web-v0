namespace DurableStack.App.Models;

public sealed class AppHomeViewModel
{
    public string DashboardStatus { get; set; } = "Connecting";

    public string DataSource { get; set; } = "User-scoped BFF";

    public string RefreshCadence { get; set; } = "Every 15s";

    public string LastUpdateLabel { get; set; } = "Not available";

    public int? TotalEvents { get; set; }

    public int? FailedEvents { get; set; }

    public string LastEventAtUtc { get; set; } = "N/A";
}
