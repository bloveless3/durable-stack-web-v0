using DurableStack.App.Menu;
using DurableStack.App.Models.Workspace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DurableStack.App.Controllers;

[Authorize]
public sealed class WorkspaceController : Controller
{
    [HttpGet("reports")]
    public IActionResult Reports()
    {
        return View("PageStub", CreateModel("Reports", AppMenuKeys.Reports, "Top-level reports workspace with summary and shortcuts."));
    }

    [HttpGet("reports/operational-health")]
    public IActionResult ReportsOperationalHealth()
    {
        return View("ReportsOperationalHealth", CreateModel("Operational Health", AppMenuKeys.ReportsOperationalHealth, "Operational health reporting surfaces will appear here."));
    }

    [HttpGet("reports/failure-analysis")]
    public IActionResult ReportsFailureAnalysis()
    {
        return View("PageStub", CreateModel("Failure Analysis", AppMenuKeys.ReportsFailureAnalysis, "Failure analysis reports will appear here."));
    }

    [HttpGet("reports/worker-health")]
    public IActionResult ReportsWorkerHealth()
    {
        return View("PageStub", CreateModel("Worker Health", AppMenuKeys.ReportsWorkerHealth, "Worker health reporting views will appear here."));
    }

    [HttpGet("reports/job-performance")]
    public IActionResult ReportsJobPerformance()
    {
        return View("PageStub", CreateModel("Job Performance", AppMenuKeys.ReportsJobPerformance, "Job performance analytics will appear here."));
    }

    [HttpGet("reports/recurring-schedules")]
    public IActionResult ReportsRecurringSchedules()
    {
        return View("PageStub", CreateModel("Recurring Schedules", AppMenuKeys.ReportsRecurringSchedules, "Recurring schedule reporting will appear here."));
    }

    [HttpGet("reports/usage-reports")]
    public IActionResult ReportsUsageReports()
    {
        return View("PageStub", CreateModel("Usage Reports", AppMenuKeys.ReportsUsageReports, "Usage reporting views will appear here."));
    }

    [HttpGet("runs")]
    public IActionResult Runs()
    {
        return View("PageStub", CreateModel("Runs", AppMenuKeys.Runs, "Run explorer workspace will appear here."));
    }

    [HttpGet("jobs")]
    public IActionResult Jobs()
    {
        return View("PageStub", CreateModel("Jobs", AppMenuKeys.Jobs, "Job definitions and management views will appear here."));
    }

    [HttpGet("workers")]
    public IActionResult Workers()
    {
        return View("PageStub", CreateModel("Workers", AppMenuKeys.Workers, "Worker inventory and diagnostics will appear here."));
    }

    [HttpGet("alerts")]
    public IActionResult Alerts()
    {
        return View("PageStub", CreateModel("Alerts", AppMenuKeys.Alerts, "Alert overview and triage tools will appear here."));
    }

    [HttpGet("alerts/active")]
    public IActionResult AlertsActive()
    {
        return View("PageStub", CreateModel("Active Alerts", AppMenuKeys.AlertsActiveAlerts, "Active alert feed will appear here."));
    }

    [HttpGet("alerts/rules")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Operator}")]
    public IActionResult AlertsRules()
    {
        return View("PageStub", CreateModel("Alert Rules", AppMenuKeys.AlertsAlertRules, "Alert rule builder will appear here."));
    }

    [HttpGet("alerts/history")]
    public IActionResult AlertsHistory()
    {
        return View("PageStub", CreateModel("Alert History", AppMenuKeys.AlertsAlertHistory, "Alert history timeline will appear here."));
    }

    [HttpGet("alerts/channels")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Operator}")]
    public IActionResult AlertsChannels()
    {
        return View("PageStub", CreateModel("Notification Channels", AppMenuKeys.AlertsNotificationChannels, "Notification channel settings will appear here."));
    }

    [HttpGet("projects")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Operator}")]
    public IActionResult Projects()
    {
        return View("PageStub", CreateModel("Projects", AppMenuKeys.Projects, "Project management workspace will appear here."));
    }

    [HttpGet("settings")]
    public IActionResult Settings()
    {
        return View("PageStub", CreateModel("Settings", AppMenuKeys.Settings, "Settings overview will appear here."));
    }

    [HttpGet("settings/organization")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Operator}")]
    public IActionResult SettingsOrganization()
    {
        return View("PageStub", CreateModel("Organization Settings", AppMenuKeys.SettingsOrganizationSettings, "Organization settings will appear here."));
    }

    [HttpGet("settings/members")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Operator}")]
    public IActionResult SettingsMembers()
    {
        return View("PageStub", CreateModel("Organization Members", AppMenuKeys.SettingsOrganizationMembers, "Organization member management will appear here."));
    }

    [HttpGet("settings/integrations")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Operator}")]
    public IActionResult SettingsIntegrations()
    {
        return View("PageStub", CreateModel("API & Integrations", AppMenuKeys.SettingsApiIntegrations, "API and integration settings will appear here."));
    }

    [HttpGet("settings/audit-log")]
    [Authorize(Roles = AppRoles.Admin)]
    public IActionResult SettingsAuditLog()
    {
        return View("PageStub", CreateModel("Audit Log", AppMenuKeys.SettingsAuditLog, "Audit log views will appear here."));
    }

    [HttpGet("settings/billing")]
    [Authorize(Roles = AppRoles.Admin)]
    public IActionResult SettingsBilling()
    {
        return View("PageStub", CreateModel("Billing (future)", AppMenuKeys.SettingsBillingFuture, "Billing workspace is planned for a future phase."));
    }

    [HttpGet("settings/preferences")]
    public IActionResult SettingsPreferences()
    {
        return View("PageStub", CreateModel("Preferences", AppMenuKeys.SettingsPreferences, "Preference settings will appear here."));
    }

    [HttpGet("settings/security")]
    [Authorize(Roles = AppRoles.Admin)]
    public IActionResult SettingsSecurity()
    {
        return View("PageStub", CreateModel("Security", AppMenuKeys.SettingsSecurity, "Security settings and controls will appear here."));
    }

    private static WorkspacePageViewModel CreateModel(string title, string menuKey, string description)
    {
        return new WorkspacePageViewModel
        {
            Title = title,
            MenuKey = menuKey,
            Description = description
        };
    }
}
