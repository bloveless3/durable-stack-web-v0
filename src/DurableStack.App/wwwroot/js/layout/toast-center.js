(function () {
  const toastRegion = document.querySelector("[data-toast-region]");

  if (!toastRegion) {
    return;
  }

  function normalizeType(type) {
    const normalized = (type || "info").toLowerCase();
    if (normalized === "success" || normalized === "error" || normalized === "warning") {
      return normalized;
    }

    return "info";
  }

  function removeToast(toast) {
    if (!toast) {
      return;
    }

    toast.classList.add("is-leaving");
    window.setTimeout(function () {
      toast.remove();
    }, 240);
  }

  function wireToast(toast) {
    const dismiss = toast.querySelector("[data-toast-dismiss]");
    const timeoutMs = Number.parseInt(toast.getAttribute("data-toast-timeout") || "0", 10);

    if (dismiss) {
      dismiss.addEventListener("click", function () {
        removeToast(toast);
      });
    }

    if (Number.isFinite(timeoutMs) && timeoutMs > 0) {
      window.setTimeout(function () {
        removeToast(toast);
      }, timeoutMs);
    }
  }

  function addToast(notification) {
    if (!notification || !notification.message) {
      return;
    }

    const toast = document.createElement("article");
    const type = normalizeType(notification.type);
    const timeout = Number.isFinite(notification.timeoutMs) && notification.timeoutMs > 0 ? notification.timeoutMs : 0;

    toast.className = `app-toast app-toast-${type}`;
    toast.setAttribute("data-toast", "");
    toast.setAttribute("data-toast-type", type);
    toast.setAttribute("data-toast-timeout", String(timeout));

    const body = document.createElement("div");
    body.className = "app-toast-body";
    body.textContent = notification.message;

    const close = document.createElement("button");
    close.type = "button";
    close.className = "app-toast-close";
    close.setAttribute("data-toast-dismiss", "");
    close.setAttribute("aria-label", "Dismiss notification");
    close.innerHTML = "&times;";

    toast.appendChild(body);
    toast.appendChild(close);
    toastRegion.appendChild(toast);

    wireToast(toast);
  }

  window.durableStackToasts = {
    showInfo: function (message, timeoutMs) {
      addToast({ type: "info", message: message, timeoutMs: timeoutMs });
    },
    showSuccess: function (message, timeoutMs) {
      addToast({ type: "success", message: message, timeoutMs: timeoutMs });
    },
    showWarning: function (message, timeoutMs) {
      addToast({ type: "warning", message: message, timeoutMs: timeoutMs });
    },
    showError: function (message, timeoutMs) {
      addToast({ type: "error", message: message, timeoutMs: timeoutMs });
    }
  };

  const existingToasts = Array.from(toastRegion.querySelectorAll("[data-toast]"));
  existingToasts.forEach(wireToast);
})();
