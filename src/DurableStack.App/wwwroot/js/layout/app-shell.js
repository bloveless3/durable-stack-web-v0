(function () {
  const activePath = (window.location.pathname || "/").toLowerCase().replace(/\/$/, "") || "/";
  const navLinks = Array.from(document.querySelectorAll(".sidebar-link[href], .top-nav-link[href]"));

  navLinks.forEach(function (link) {
    const hrefValue = (link.getAttribute("href") || "").trim().toLowerCase();
    if (!hrefValue.startsWith("/")) {
      return;
    }

    const normalizedHref = hrefValue.replace(/\/$/, "") || "/";
    if (normalizedHref === activePath) {
      link.classList.add("is-active");
    }
  });
})();
