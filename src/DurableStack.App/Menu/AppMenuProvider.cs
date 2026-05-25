using System.Security.Claims;

namespace DurableStack.App.Menu;

public sealed class AppMenuProvider : IAppMenuProvider
{
    private static readonly string[] OperatorAndAbove = [AppRoles.Admin, AppRoles.Operator];
    private static readonly string[] AdminOnly = [AppRoles.Admin];

    private static readonly IReadOnlyList<AppMenuItem> MenuItems =
    [
        new AppMenuItem
        {
            Key = AppMenuKeys.Dashboard,
            Title = "Dashboard",
            Url = "/",
            IconClass = "fa-solid fa-table-columns",
            AllowedRoles = AppRoles.All
        },
        new AppMenuItem
        {
            Key = AppMenuKeys.Reports,
            Title = "Reports",
            Url = "/reports",
            IconClass = "fa-solid fa-chart-line",
            AllowedRoles = AppRoles.All,
            Children =
            [
                CreateChild(AppMenuKeys.ReportsOperationalHealth, "Operational Health", "/reports/operational-health", AppRoles.All),
                CreateChild(AppMenuKeys.ReportsFailureAnalysis, "Failure Analysis", "/reports/failure-analysis", AppRoles.All),
                CreateChild(AppMenuKeys.ReportsWorkerHealth, "Worker Health", "/reports/worker-health", AppRoles.All),
                CreateChild(AppMenuKeys.ReportsJobPerformance, "Job Performance", "/reports/job-performance", AppRoles.All),
                CreateChild(AppMenuKeys.ReportsRecurringSchedules, "Recurring Schedules", "/reports/recurring-schedules", AppRoles.All),
                CreateChild(AppMenuKeys.ReportsUsageReports, "Usage Reports", "/reports/usage-reports", AppRoles.All)
            ]
        },
        new AppMenuItem
        {
            Key = AppMenuKeys.Runs,
            Title = "Runs",
            Url = "/runs",
            IconClass = "fa-solid fa-play",
            AllowedRoles = AppRoles.All
        },
        new AppMenuItem
        {
            Key = AppMenuKeys.Jobs,
            Title = "Jobs",
            Url = "/jobs",
            IconClass = "fa-solid fa-briefcase",
            AllowedRoles = AppRoles.All
        },
        new AppMenuItem
        {
            Key = AppMenuKeys.Workers,
            Title = "Workers",
            Url = "/workers",
            IconClass = "fa-solid fa-server",
            AllowedRoles = AppRoles.All
        },
        new AppMenuItem
        {
            Key = AppMenuKeys.Alerts,
            Title = "Alerts",
            Url = "/alerts",
            IconClass = "fa-solid fa-bell",
            AllowedRoles = AppRoles.All,
            Children =
            [
                CreateChild(AppMenuKeys.AlertsActiveAlerts, "Active Alerts", "/alerts/active", AppRoles.All),
                CreateChild(AppMenuKeys.AlertsAlertRules, "Alert Rules", "/alerts/rules", OperatorAndAbove),
                CreateChild(AppMenuKeys.AlertsAlertHistory, "Alert History", "/alerts/history", AppRoles.All),
                CreateChild(AppMenuKeys.AlertsNotificationChannels, "Notification Channels", "/alerts/channels", OperatorAndAbove)
            ]
        },
        new AppMenuItem
        {
            Key = AppMenuKeys.Projects,
            Title = "Projects",
            Url = "/projects",
            IconClass = "fa-solid fa-folder-tree",
            AllowedRoles = OperatorAndAbove
        },
        new AppMenuItem
        {
            Key = AppMenuKeys.Settings,
            Title = "Settings",
            Url = "/settings",
            IconClass = "fa-solid fa-gear",
            AllowedRoles = AppRoles.All,
            Children =
            [
                CreateChild(AppMenuKeys.SettingsOrganizationSettings, "Organization Settings", "/settings/organization", OperatorAndAbove),
                CreateChild(AppMenuKeys.SettingsOrganizationMembers, "Organization Members", "/settings/members", OperatorAndAbove),
                CreateChild(AppMenuKeys.SettingsApiIntegrations, "API & Integrations", "/settings/integrations", OperatorAndAbove),
                CreateChild(AppMenuKeys.SettingsAuditLog, "Audit Log", "/settings/audit-log", AdminOnly),
                CreateChild(AppMenuKeys.SettingsBillingFuture, "Billing (future)", "/settings/billing", AdminOnly),
                CreateChild(AppMenuKeys.SettingsPreferences, "Preferences", "/settings/preferences", AppRoles.All),
                CreateChild(AppMenuKeys.SettingsSecurity, "Security", "/settings/security", AdminOnly)
            ]
        }
    ];

    public IReadOnlyList<AppMenuItem> GetMenu(ClaimsPrincipal user)
    {
        var roles = user
            .FindAll(ClaimTypes.Role)
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (roles.Count == 0)
        {
            roles.Add(AppRoles.Viewer);
        }

        static bool IsAllowed(AppMenuItem item, ISet<string> rolesSet)
        {
            if (item.AllowedRoles.Count == 0)
            {
                return true;
            }

            return item.AllowedRoles.Any(role => rolesSet.Contains(role));
        }

        static AppMenuItem? FilterItem(AppMenuItem item, ISet<string> rolesSet)
        {
            var filteredChildren = item.Children
                .Select(child => FilterItem(child, rolesSet))
                .Where(child => child is not null)
                .Cast<AppMenuItem>()
                .ToList();

            var selfAllowed = IsAllowed(item, rolesSet);
            if (!selfAllowed && filteredChildren.Count == 0)
            {
                return null;
            }

            return new AppMenuItem
            {
                Key = item.Key,
                Title = item.Title,
                Url = item.Url,
                IconClass = item.IconClass,
                AllowedRoles = item.AllowedRoles,
                Children = filteredChildren
            };
        }

        return MenuItems
            .Select(item => FilterItem(item, roles))
            .Where(item => item is not null)
            .Cast<AppMenuItem>()
            .ToList();
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

    private static AppMenuItem CreateChild(string key, string title, string url, IReadOnlyList<string> allowedRoles)
    {
        return new AppMenuItem
        {
            Key = key,
            Title = title,
            Url = url,
            AllowedRoles = allowedRoles
        };
    }
}
