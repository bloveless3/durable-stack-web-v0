(function () {
  const keys = {
    uiSidebarCompact: "ui.sidebar.compact",
    globalFilterOrganization: "global.filter.organization",
    globalFilterProject: "global.filter.project",
    globalFilterEnvironment: "global.filter.environment",
    globalFilterTimeRange: "global.filter.timeRange"
  };

  keys.globalFilterAll = [
    keys.globalFilterOrganization,
    keys.globalFilterProject,
    keys.globalFilterEnvironment,
    keys.globalFilterTimeRange
  ];

  window.durableStackPreferenceKeys = keys;
})();
