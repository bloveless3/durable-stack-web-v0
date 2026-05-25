(function () {
  const frame = document.querySelector("[data-app-frame]");
  const sidebar = document.querySelector("[data-sidebar]");
  const openTrigger = document.querySelector("[data-sidebar-open-trigger]");
  const closeTriggers = Array.from(document.querySelectorAll("[data-sidebar-close-trigger]"));
  const themeToggle = document.querySelector("[data-theme-toggle]");
  const compactToggle = document.querySelector("[data-sidebar-compact-toggle]");
  const compactToggleIcon = compactToggle ? compactToggle.querySelector("i") : null;
  const flyout = document.querySelector("[data-sidebar-flyout]");
  const desktopMediaQuery = window.matchMedia("(min-width: 1024px)");

  const activePath = (window.location.pathname || "/").toLowerCase().replace(/\/$/, "") || "/";
  const navLinks = Array.from(document.querySelectorAll(".sidebar-link[href], .sidebar-sublink[href]"));
  const menuGroups = Array.from(document.querySelectorAll("[data-menu-group]"));
  const hasServerSelectedMenu = document.querySelector(".sidebar-link.is-active, .sidebar-sublink.is-active") !== null;

  function isCompactMode() {
    return frame && frame.getAttribute("data-sidebar-compact") === "true";
  }

  function getFlyoutActiveKey() {
    return flyout ? flyout.getAttribute("data-active-menu") : null;
  }

  function getMenuTriggerByKey(key) {
    if (!key) {
      return null;
    }

    return document.querySelector(`[data-menu-group][data-menu-key="${key}"] [data-menu-trigger]`);
  }

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

  function hideFlyout() {
    if (!flyout) {
      return null;
    }

    const activeKey = getFlyoutActiveKey();
    if (activeKey && isCompactMode()) {
      const ownerTrigger = getMenuTriggerByKey(activeKey);
      if (ownerTrigger) {
        ownerTrigger.setAttribute("aria-expanded", "false");
      }
    }

    flyout.hidden = true;
    flyout.innerHTML = "";
    flyout.removeAttribute("data-active-menu");
    flyout.removeAttribute("aria-labelledby");

    return activeKey;
  }

  function focusFlyoutLink(position) {
    if (!flyout || flyout.hidden) {
      return;
    }

    const links = Array.from(flyout.querySelectorAll("a.sidebar-sublink[href]"));
    if (links.length === 0) {
      return;
    }

    if (position === "last") {
      links[links.length - 1].focus();
      return;
    }

    links[0].focus();
  }

  function focusFlyoutOwner(activeKey) {
    const ownerTrigger = getMenuTriggerByKey(activeKey);
    if (ownerTrigger) {
      ownerTrigger.focus();
    }
  }

  function showFlyoutForGroup(group, trigger) {
    if (!flyout || !sidebar) {
      return;
    }

    const groupKey = group.getAttribute("data-menu-key") || "";
    const previousActiveKey = getFlyoutActiveKey();
    if (previousActiveKey && previousActiveKey !== groupKey) {
      const previousTrigger = getMenuTriggerByKey(previousActiveKey);
      if (previousTrigger) {
        previousTrigger.setAttribute("aria-expanded", "false");
      }
    }

    const submenu = group.querySelector("[data-menu-panel]");
    if (!submenu) {
      hideFlyout();
      return;
    }

    const clone = submenu.cloneNode(true);
    clone.classList.add("is-flyout");
    clone.classList.remove("sidebar-submenu");
    clone.id = `flyout-submenu-${groupKey || "menu"}`;
    clone.setAttribute("role", "menu");
    clone.querySelectorAll("a.sidebar-sublink").forEach(function (link) {
      link.setAttribute("role", "menuitem");
      link.setAttribute("tabindex", "-1");
    });

    const menuTitle = trigger.getAttribute("data-menu-title") || trigger.querySelector(".sidebar-link-text")?.textContent || "Menu";

    flyout.innerHTML = "";

    const heading = document.createElement("p");
    heading.className = "sidebar-flyout-title";
    heading.textContent = menuTitle;
    heading.id = `${clone.id}-title`;

    flyout.appendChild(heading);
    flyout.appendChild(clone);
    flyout.setAttribute("aria-labelledby", heading.id);

    flyout.hidden = false;
    flyout.style.visibility = "hidden";

    const triggerRect = trigger.getBoundingClientRect();
    const sidebarRect = sidebar.getBoundingClientRect();
    const flyoutRect = flyout.getBoundingClientRect();
    const viewportPadding = 8;
    const headerHeight = Number.parseFloat(getComputedStyle(document.documentElement).getPropertyValue("--header-height"));
    const minTop = (Number.isNaN(headerHeight) ? 60 : headerHeight) + viewportPadding;
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
    flyout.setAttribute("data-active-menu", groupKey);

    trigger.setAttribute("aria-expanded", "true");
  }

  function toggleFlyoutForGroup(group, trigger, focusPosition) {
    const groupKey = group.getAttribute("data-menu-key") || "";
    const isVisible = flyout && !flyout.hidden && getFlyoutActiveKey() === groupKey;
    if (isVisible) {
      hideFlyout();
      return;
    }

    showFlyoutForGroup(group, trigger);
    if (focusPosition) {
      focusFlyoutLink(focusPosition);
    }
  }

  function applyCompactState(isCompact) {
    if (!frame) {
      return;
    }

    const shouldCompact = isCompact && desktopMediaQuery.matches;
    frame.setAttribute("data-sidebar-compact", String(shouldCompact));

    if (compactToggle) {
      compactToggle.setAttribute("aria-pressed", String(shouldCompact));
    }

    updateCompactToggleUi(shouldCompact);

    if (!shouldCompact) {
      hideFlyout();
    }
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

  function handleTriggerKeydown(event, group, trigger) {
    if (isCompactMode()) {
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        toggleFlyoutForGroup(group, trigger, "first");
        return;
      }

      if (event.key === "ArrowDown") {
        event.preventDefault();
        toggleFlyoutForGroup(group, trigger, "first");
        return;
      }

      if (event.key === "ArrowUp") {
        event.preventDefault();
        toggleFlyoutForGroup(group, trigger, "last");
        return;
      }
    }

    if (event.key === "ArrowRight") {
      event.preventDefault();
      setGroupExpanded(group, true);
      return;
    }

    if (event.key === "ArrowLeft") {
      event.preventDefault();
      setGroupExpanded(group, false);
    }
  }

  function handleFlyoutLinkKeydown(event) {
    if (!flyout || flyout.hidden) {
      return;
    }

    const target = event.target;
    if (!(target instanceof Element) || !target.matches(".sidebar-flyout .sidebar-sublink")) {
      return;
    }

    const links = Array.from(flyout.querySelectorAll("a.sidebar-sublink[href]"));
    const currentIndex = links.indexOf(target);
    if (currentIndex < 0) {
      return;
    }

    if (event.key === "ArrowDown") {
      event.preventDefault();
      links[(currentIndex + 1) % links.length].focus();
      return;
    }

    if (event.key === "ArrowUp") {
      event.preventDefault();
      links[(currentIndex - 1 + links.length) % links.length].focus();
      return;
    }

    if (event.key === "Home") {
      event.preventDefault();
      links[0].focus();
      return;
    }

    if (event.key === "End") {
      event.preventDefault();
      links[links.length - 1].focus();
      return;
    }

    if (event.key === "ArrowLeft") {
      event.preventDefault();
      const activeKey = hideFlyout();
      if (activeKey) {
        focusFlyoutOwner(activeKey);
      }
    }
  }

  function handleDocumentKeydown(event) {
    if (event.key === "Escape") {
      const target = event.target;
      const shouldRestoreFocus = target instanceof Element && target.closest("[data-sidebar-flyout]");
      const activeKey = hideFlyout();
      if (shouldRestoreFocus && activeKey) {
        focusFlyoutOwner(activeKey);
      }

      setSidebarOpen(false);
      return;
    }

    handleFlyoutLinkKeydown(event);
  }

  function handleResize() {
    if (desktopMediaQuery.matches) {
      setSidebarOpen(false);
    }

    hideFlyout();
    applyCompactState(getCompactPreference());
  }

  if (openTrigger && frame) {
    openTrigger.addEventListener("click", function () {
      const isOpen = frame.getAttribute("data-sidebar-open") === "true";
      setSidebarOpen(!isOpen);
    });
  }

  closeTriggers.forEach(function (trigger) {
    trigger.addEventListener("click", function () {
      setSidebarOpen(false);
    });
  });

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
      if (isCompactMode()) {
        toggleFlyoutForGroup(group, trigger);
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
      handleTriggerKeydown(event, group, trigger);
    });
  });

  navLinks.forEach(function (link) {
    link.addEventListener("click", function () {
      if (!desktopMediaQuery.matches) {
        setSidebarOpen(false);
      }
    });
  });

  document.addEventListener("click", function (event) {
    if (!flyout || flyout.hidden) {
      return;
    }

    const target = event.target;
    if (!(target instanceof Element)) {
      hideFlyout();
      return;
    }

    if (flyout.contains(target)) {
      return;
    }

    if (target.closest("[data-menu-trigger]")) {
      return;
    }

    hideFlyout();
  });

  document.addEventListener("keydown", handleDocumentKeydown);
  window.addEventListener("resize", handleResize);

  handleResize();

  if (window.durableStackPreferences && window.durableStackPreferenceKeys) {
    window.durableStackPreferences
      .get(window.durableStackPreferenceKeys.uiSidebarCompact)
      .then(function (serverValue) {
        if (serverValue === "true" || serverValue === "false") {
          localStorage.setItem(window.durableStackPreferenceKeys.uiSidebarCompact, serverValue);
          applyCompactState(serverValue === "true");
        }
      })
      .catch(function () {
        // Preference sync is best-effort.
      });
  }
})();
