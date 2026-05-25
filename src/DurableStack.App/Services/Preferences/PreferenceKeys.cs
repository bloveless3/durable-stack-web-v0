namespace DurableStack.App.Services.Preferences;

public static class PreferenceKeys
{
    public const string UiSidebarCompact = "ui.sidebar.compact";

    public const string GlobalFilterOrganization = "global.filter.organization";
    public const string GlobalFilterProject = "global.filter.project";
    public const string GlobalFilterEnvironment = "global.filter.environment";
    public const string GlobalFilterTimeRange = "global.filter.timeRange";

    public static readonly IReadOnlyList<string> GlobalFilterKeys =
    [
        GlobalFilterOrganization,
        GlobalFilterProject,
        GlobalFilterEnvironment,
        GlobalFilterTimeRange
    ];
}
