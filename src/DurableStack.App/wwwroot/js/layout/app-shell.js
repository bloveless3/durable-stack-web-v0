(function () {
  const frame = document.querySelector("[data-app-frame]");
  const openTrigger = document.querySelector("[data-sidebar-open-trigger]");
  const closeTriggers = Array.from(document.querySelectorAll("[data-sidebar-close-trigger]"));
  const themeToggle = document.querySelector("[data-theme-toggle]");

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

  navLinks.forEach(function (link) {
    link.addEventListener("click", function () {
      if (!window.matchMedia("(min-width: 1024px)").matches) {
        setSidebarOpen(false);
      }
    });
  });
})();
