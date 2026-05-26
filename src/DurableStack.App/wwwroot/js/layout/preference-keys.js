(function () {
  const keys = {
    uiSidebarCompact: "ui.sidebar.compact",
    globalFilterOrganization: "global.filter.organization",
    globalFilterProject: "global.filter.project",
    globalFilterTenant: "global.filter.tenant",
    globalFilterTimeRange: "global.filter.timeRange"
  };

  keys.globalFilterAll = [
    keys.globalFilterOrganization,
    keys.globalFilterProject,
    keys.globalFilterTenant,
    keys.globalFilterTimeRange
  ];

  window.durableStackPreferenceKeys = keys;
})();
