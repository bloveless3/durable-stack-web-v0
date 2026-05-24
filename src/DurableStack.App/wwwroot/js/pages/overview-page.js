(function () {
  const refreshTrigger = document.querySelector("[data-dashboard-refresh]");
  const refreshTooltip = document.querySelector("[data-dashboard-refresh-tooltip]");

  function formatLastLoadLabel(dateValue) {
    const hours24 = dateValue.getHours();
    const minutes = dateValue.getMinutes().toString().padStart(2, "0");
    const meridiem = hours24 >= 12 ? "pm" : "am";
    const hours12 = hours24 % 12 || 12;
    return `Last load ${hours12}:${minutes}${meridiem}`;
  }

  function setLastLoaded() {
    if (!refreshTrigger || !refreshTooltip) {
      return;
    }

    const label = formatLastLoadLabel(new Date());
    refreshTooltip.setAttribute("data-tip", label);
  }

  if (refreshTrigger) {
    refreshTrigger.addEventListener("click", function () {
      setLastLoaded();
      refreshTrigger.blur();
    });
  }

  setLastLoaded();
})();
