(function () {
  const frame = document.querySelector("[data-app-frame]");
  const sidebar = document.querySelector("[data-sidebar]");
  const openTrigger = document.querySelector("[data-sidebar-open-trigger]");
  const closeTriggers = Array.from(document.querySelectorAll("[data-sidebar-close-trigger]"));
  const themeToggle = document.querySelector("[data-theme-toggle]");
  const compactToggle = document.querySelector("[data-sidebar-compact-toggle]");
  const compactToggleIcon = compactToggle ? compactToggle.querySelector("i") : null;
  const flyout = document.querySelector("[data-sidebar-flyout]");

  function updateCompactToggleUi(isCompact) {
    if (!compactToggle) {
      return;
    }

    const label = isCompact ? "Expand navigation" : "Compact navigation";
    compactToggle.setAttribute("title", label);
    compactToggle.setAttribute("aria-label", label);

    if (!compactToggleIcon) {
      return;
    }

    compactToggleIcon.classList.remove("fa-compress", "fa-expand");
    compactToggleIcon.classList.add(isCompact ? "fa-expand" : "fa-compress");
  }

  function getCompactPreference() {
    if (!window.durableStackPreferenceKeys) {
      return false;
    }

    return localStorage.getItem(window.durableStackPreferenceKeys.uiSidebarCompact) === "true";
  }

  function setCompactPreference(isCompact) {
    if (!window.durableStackPreferenceKeys) {
      return;
    }

    localStorage.setItem(window.durableStackPreferenceKeys.uiSidebarCompact, String(isCompact));

    if (window.durableStackPreferences) {
      window.durableStackPreferences.set(window.durableStackPreferenceKeys.uiSidebarCompact, String(isCompact)).catch(function () {
        if (window.durableStackToasts) {
          window.durableStackToasts.showWarning("Sidebar mode could not be synced.", 3500);
        }
      });
    }
  }

  function applyCompactState(isCompact) {
    if (!frame) {
      return;
    }

    const shouldCompact = isCompact && window.matchMedia("(min-width: 1024px)").matches;
    frame.setAttribute("data-sidebar-compact", String(shouldCompact));

    if (compactToggle) {
      compactToggle.setAttribute("aria-pressed", String(shouldCompact));
    }

    updateCompactToggleUi(shouldCompact);

    if (!shouldCompact) {
      hideFlyout();
    }
  }

  function hideFlyout() {
    if (!flyout) {
      return;
    }

    flyout.hidden = true;
    flyout.innerHTML = "";
  }

  function showFlyoutForGroup(group, trigger) {
    if (!flyout || !sidebar) {
      return;
    }

    const submenu = group.querySelector("[data-menu-panel]");
    if (!submenu) {
      hideFlyout();
      return;
    }

    const clone = submenu.cloneNode(true);
    clone.classList.add("is-flyout");
    clone.classList.remove("sidebar-submenu");

    const title = trigger.querySelector(".sidebar-link-text");
    const titleText = title ? title.textContent : "Menu";

    flyout.innerHTML = "";

    const heading = document.createElement("p");
    heading.className = "sidebar-flyout-title";
    heading.textContent = titleText || "Menu";

    flyout.appendChild(heading);
    flyout.appendChild(clone);

    flyout.hidden = false;
    flyout.style.visibility = "hidden";

    const triggerRect = trigger.getBoundingClientRect();
    const sidebarRect = sidebar.getBoundingClientRect();
    const flyoutRect = flyout.getBoundingClientRect();

    const viewportPadding = 8;
    const minTop = Number.parseFloat(getComputedStyle(document.documentElement).getPropertyValue("--header-height")) + viewportPadding || 68;
    const maxTop = window.innerHeight - flyoutRect.height - viewportPadding;
    const clampedTop = Math.max(minTop, Math.min(triggerRect.top, maxTop));

    let left = sidebarRect.right + 8;
    if (left + flyoutRect.width > window.innerWidth - viewportPadding) {
      left = Math.max(viewportPadding, sidebarRect.left - flyoutRect.width - 8);
    }

    flyout.style.top = `${clampedTop}px`;
    flyout.style.left = `${left}px`;
    flyout.style.visibility = "";
    flyout.hidden = false;
  }

  function getTheme() {
    return document.documentElement.getAttribute("data-theme") === "dark" ? "dark" : "light";
  }

  function applyTheme(theme) {
    const normalizedTheme = theme === "dark" ? "dark" : "light";
    document.documentElement.setAttribute("data-theme", normalizedTheme);
    localStorage.setItem("durablestack-theme", normalizedTheme);

    if (themeToggle) {
      const isDark = normalizedTheme === "dark";
      themeToggle.setAttribute("aria-pressed", String(isDark));
      themeToggle.textContent = isDark ? "Dark" : "Light";
    }
  }

  function setSidebarOpen(isOpen) {
    if (!frame) {
      return;
    }

    frame.setAttribute("data-sidebar-open", String(isOpen));

    if (openTrigger) {
      openTrigger.setAttribute("aria-expanded", String(isOpen));
    }
  }

  function handleResize() {
    if (window.matchMedia("(min-width: 1024px)").matches) {
      setSidebarOpen(false);
    }

    applyCompactState(getCompactPreference());
  }

  if (openTrigger && frame) {
    openTrigger.addEventListener("click", function () {
      const isOpen = frame.getAttribute("data-sidebar-open") === "true";
      setSidebarOpen(!isOpen);
    });
  }

  if (themeToggle) {
    themeToggle.addEventListener("click", function () {
      applyTheme(getTheme() === "dark" ? "light" : "dark");
    });
    applyTheme(getTheme());
  }

  if (compactToggle) {
    compactToggle.addEventListener("click", function () {
      const next = !getCompactPreference();
      setCompactPreference(next);
      applyCompactState(next);
    });
  }

  closeTriggers.forEach(function (trigger) {
    trigger.addEventListener("click", function () {
      setSidebarOpen(false);
    });
  });

  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape") {
      setSidebarOpen(false);
    }
  });

  window.addEventListener("resize", handleResize);
  handleResize();

  if (window.durableStackPreferences && window.durableStackPreferenceKeys) {
    window.durableStackPreferences.get(window.durableStackPreferenceKeys.uiSidebarCompact).then(function (serverValue) {
      if (serverValue === "true" || serverValue === "false") {
        localStorage.setItem(window.durableStackPreferenceKeys.uiSidebarCompact, serverValue);
        applyCompactState(serverValue === "true");
      }
    }).catch(function () {
      // Preference sync is best-effort.
    });
  }

  const activePath = (window.location.pathname || "/").toLowerCase().replace(/\/$/, "") || "/";
  const navLinks = Array.from(document.querySelectorAll(".sidebar-link[href], .sidebar-sublink[href]"));
  const menuGroups = Array.from(document.querySelectorAll("[data-menu-group]"));
  const hasServerSelectedMenu = document.querySelector(".sidebar-link.is-active, .sidebar-sublink.is-active") !== null;

  function setGroupExpanded(group, isExpanded) {
    group.classList.toggle("is-expanded", isExpanded);

    const trigger = group.querySelector("[data-menu-trigger]");
    if (trigger) {
      trigger.setAttribute("aria-expanded", String(isExpanded));
    }
  }

  function collapseAllGroupsExcept(exceptionGroup) {
    menuGroups.forEach(function (group) {
      if (group === exceptionGroup) {
        return;
      }

      if (!group.classList.contains("is-active")) {
        setGroupExpanded(group, false);
      }
    });
  }

  if (!hasServerSelectedMenu) {
    navLinks.forEach(function (link) {
      const hrefValue = (link.getAttribute("href") || "").trim().toLowerCase();
      if (!hrefValue.startsWith("/")) {
        return;
      }

      const normalizedHref = hrefValue.replace(/\/$/, "") || "/";
      if (normalizedHref === activePath) {
        link.classList.add("is-active");

        const parentGroup = link.closest(".sidebar-nav-group");
        if (parentGroup) {
          parentGroup.classList.add("is-active");
          const parentLink = parentGroup.querySelector(":scope > .sidebar-link");
          if (parentLink) {
            parentLink.classList.add("is-active");
          }
        }
      }
    });
  }

  menuGroups.forEach(function (group) {
    const trigger = group.querySelector("[data-menu-trigger]");
    if (!trigger) {
      return;
    }

    trigger.addEventListener("click", function () {
      const isCompact = frame && frame.getAttribute("data-sidebar-compact") === "true";
      if (isCompact) {
        const isVisible = !flyout?.hidden && flyout?.getAttribute("data-active-menu") === group.getAttribute("data-menu-key");
        if (isVisible) {
          hideFlyout();
          return;
        }

        showFlyoutForGroup(group, trigger);
        if (flyout) {
          flyout.setAttribute("data-active-menu", group.getAttribute("data-menu-key") || "");
        }
        return;
      }

      const isExpanded = group.classList.contains("is-expanded");
      const nextState = !isExpanded;

      if (nextState) {
        collapseAllGroupsExcept(group);
      }

      setGroupExpanded(group, nextState);
    });

    trigger.addEventListener("keydown", function (event) {
      if (event.key === "ArrowRight") {
        event.preventDefault();
        setGroupExpanded(group, true);
      }

      if (event.key === "ArrowLeft") {
        event.preventDefault();
        setGroupExpanded(group, false);
      }
    });
  });

  document.addEventListener("click", function (event) {
    if (!flyout || flyout.hidden) {
      return;
    }

    const target = event.target;
    if (flyout.contains(target)) {
      return;
    }

    const inTrigger = target.closest && target.closest("[data-menu-trigger]");
    if (inTrigger) {
      return;
    }

    hideFlyout();
  });

  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape") {
      hideFlyout();
    }
  });

  window.addEventListener("resize", hideFlyout);

  navLinks.forEach(function (link) {
    link.addEventListener("click", function () {
      if (!window.matchMedia("(min-width: 1024px)").matches) {
        setSidebarOpen(false);
      }
    });
  });
})();
