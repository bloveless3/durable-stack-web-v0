(function () {
  const filterRoot = document.querySelector("[data-global-filters]");
  if (!filterRoot || !window.durableStackPreferences) {
    return;
  }

  const controls = Array.from(filterRoot.querySelectorAll("[data-global-filter-key]"));

  controls.forEach(function (control) {
    control.addEventListener("change", async function () {
      const key = control.getAttribute("data-global-filter-key");
      const value = control.value;

      if (!key) {
        return;
      }

      try {
        await window.durableStackPreferences.set(key, value);
      } catch (error) {
        if (window.durableStackToasts) {
          window.durableStackToasts.showError("Could not save filter preference.", 5000);
        }
      }
    });
  });
})();
