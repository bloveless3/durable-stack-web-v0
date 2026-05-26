(function () {
  if (!window.durableStackPreferences || !window.durableStackPreferenceKeys) {
    return;
  }

  const keys = window.durableStackPreferenceKeys;
  const defaults = {
    [keys.globalFilterOrganization]: "all-organizations",
    [keys.globalFilterProject]: "all-projects",
    [keys.globalFilterTenant]: "all-tenants",
    [keys.globalFilterTimeRange]: "24h"
  };

  function toState(preferenceMap) {
    return {
      organization: preferenceMap[keys.globalFilterOrganization] || defaults[keys.globalFilterOrganization],
      project: preferenceMap[keys.globalFilterProject] || defaults[keys.globalFilterProject],
      tenant: preferenceMap[keys.globalFilterTenant] || defaults[keys.globalFilterTenant],
      timeRange: preferenceMap[keys.globalFilterTimeRange] || defaults[keys.globalFilterTimeRange]
    };
  }

  function readDom(root) {
    const output = {};
    const controls = Array.from(root.querySelectorAll("[data-global-filter-key]"));

    controls.forEach(function (control) {
      const key = control.getAttribute("data-global-filter-key");
      if (!key) {
        return;
      }

      output[key] = control.value;
    });

    return output;
  }

  function applyDom(root, state) {
    const controls = Array.from(root.querySelectorAll("[data-global-filter-key]"));

    controls.forEach(function (control) {
      const key = control.getAttribute("data-global-filter-key");
      if (!key || !(key in state)) {
        return;
      }

      control.value = state[key];
    });
  }

  function emitChanged(detail) {
    document.dispatchEvent(new CustomEvent("durablestack:filters-changed", { detail: detail }));
  }

  async function getCurrent() {
    const preferenceMap = await window.durableStackPreferences.getMany(keys.globalFilterAll);
    const mapWithDefaults = {
      ...defaults,
      ...preferenceMap
    };

    return {
      byKey: mapWithDefaults,
      state: toState(mapWithDefaults)
    };
  }

  async function setByKey(key, value) {
    if (!key) {
      return;
    }

    await window.durableStackPreferences.set(key, value);
    const current = await getCurrent();
    emitChanged(current);
    return current;
  }

  window.durableStackFilters = {
    keys: keys,
    defaults: defaults,
    getCurrent: getCurrent,
    setByKey: setByKey,
    readDom: readDom,
    applyDom: applyDom
  };
})();
