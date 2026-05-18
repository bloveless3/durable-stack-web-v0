(function () {
  const overlay = document.querySelector("[data-command-overlay]");
  const openButtons = Array.from(document.querySelectorAll("[data-command-open]"));
  const closeButtons = Array.from(document.querySelectorAll("[data-command-close]"));
  const input = document.getElementById("command-input");

  if (!overlay) {
    return;
  }

  function openPalette() {
    overlay.hidden = false;
    document.body.style.overflow = "hidden";
    if (input) {
      window.setTimeout(function () { input.focus(); }, 0);
    }
  }

  function closePalette() {
    overlay.hidden = true;
    document.body.style.overflow = "";
  }

  openButtons.forEach(function (button) {
    button.addEventListener("click", openPalette);
  });

  closeButtons.forEach(function (button) {
    button.addEventListener("click", closePalette);
  });

  overlay.addEventListener("click", function (event) {
    if (event.target === overlay) {
      closePalette();
    }
  });

  document.addEventListener("keydown", function (event) {
    if (event.key.toLowerCase() === "k" && event.ctrlKey) {
      event.preventDefault();
      if (overlay.hidden) {
        openPalette();
      } else {
        closePalette();
      }
    }

    if (event.key === "Escape" && !overlay.hidden) {
      closePalette();
    }
  });
})();
