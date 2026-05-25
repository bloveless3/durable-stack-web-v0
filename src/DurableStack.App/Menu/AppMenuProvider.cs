namespace DurableStack.App.Menu;

public sealed class AppMenuProvider : IAppMenuProvider
{
    private static readonly IReadOnlyList<AppMenuItem> MenuItems =
    [
        new AppMenuItem
        {
            Key = AppMenuKeys.Dashboard,
            Title = "Dashboard",
            Url = "/",
            IconClass = "fa-solid fa-table-columns"
        },
        new AppMenuItem
        {
            Key = AppMenuKeys.Reports,
            Title = "Reports",
            Url = "/reports",
            IconClass = "fa-solid fa-chart-line",
            Children =
            [
                CreateChild(AppMenuKeys.ReportsOperationalHealth, "Operational Health", "/reports/operational-health"),
                CreateChild(AppMenuKeys.ReportsFailureAnalysis, "Failure Analysis", "/reports/failure-analysis"),
                CreateChild(AppMenuKeys.ReportsWorkerHealth, "Worker Health", "/reports/worker-health"),
                CreateChild(AppMenuKeys.ReportsJobPerformance, "Job Performance", "/reports/job-performance"),
                CreateChild(AppMenuKeys.ReportsRecurringSchedules, "Recurring Schedules", "/reports/recurring-schedules"),
                CreateChild(AppMenuKeys.ReportsUsageReports, "Usage Reports", "/reports/usage-reports")
            ]
        },
        new AppMenuItem
        {
            Key = AppMenuKeys.Runs,
            Title = "Runs",
            Url = "/runs",
            IconClass = "fa-solid fa-play"
        },
        new AppMenuItem
        {
            Key = AppMenuKeys.Jobs,
            Title = "Jobs",
            Url = "/jobs",
            IconClass = "fa-solid fa-briefcase"
        },
        new AppMenuItem
        {
            Key = AppMenuKeys.Workers,
            Title = "Workers",
            Url = "/workers",
            IconClass = "fa-solid fa-server"
        },
        new AppMenuItem
        {
            Key = AppMenuKeys.Alerts,
            Title = "Alerts",
            Url = "/alerts",
            IconClass = "fa-solid fa-bell",
            Children =
            [
                CreateChild(AppMenuKeys.AlertsActiveAlerts, "Active Alerts", "/alerts/active"),
                CreateChild(AppMenuKeys.AlertsAlertRules, "Alert Rules", "/alerts/rules"),
                CreateChild(AppMenuKeys.AlertsAlertHistory, "Alert History", "/alerts/history"),
                CreateChild(AppMenuKeys.AlertsNotificationChannels, "Notification Channels", "/alerts/channels")
            ]
        },
        new AppMenuItem
        {
            Key = AppMenuKeys.Projects,
            Title = "Projects",
            Url = "/projects",
            IconClass = "fa-solid fa-folder-tree"
        },
        new AppMenuItem
        {
            Key = AppMenuKeys.Settings,
            Title = "Settings",
            Url = "/settings",
            IconClass = "fa-solid fa-gear",
            Children =
            [
                CreateChild(AppMenuKeys.SettingsOrganizationSettings, "Organization Settings", "/settings/organization"),
                CreateChild(AppMenuKeys.SettingsOrganizationMembers, "Organization Members", "/settings/members"),
                CreateChild(AppMenuKeys.SettingsApiIntegrations, "API & Integrations", "/settings/integrations"),
                CreateChild(AppMenuKeys.SettingsAuditLog, "Audit Log", "/settings/audit-log"),
                CreateChild(AppMenuKeys.SettingsBillingFuture, "Billing (future)", "/settings/billing"),
                CreateChild(AppMenuKeys.SettingsPreferences, "Preferences", "/settings/preferences"),
                CreateChild(AppMenuKeys.SettingsSecurity, "Security", "/settings/security")
            ]
        }
    ];

    public IReadOnlyList<AppMenuItem> GetMenu()
    {
        return MenuItems;
    }

    public AppMenuItem? FindByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        static AppMenuItem? Traverse(IEnumerable<AppMenuItem> items, string target)
        {
            foreach (var item in items)
            {
                if (string.Equals(item.Key, target, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }

                var childHit = Traverse(item.Children, target);
                if (childHit is not null)
                {
                    return childHit;
                }
            }

            return null;
        }

        return Traverse(MenuItems, key.Trim());
    }

    private static AppMenuItem CreateChild(string key, string title, string url)
    {
        return new AppMenuItem
        {
            Key = key,
            Title = title,
            Url = url
        };
    }
}
