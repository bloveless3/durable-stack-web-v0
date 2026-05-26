(function () {
  const refreshTrigger = document.querySelector("[data-dashboard-refresh]");
  const refreshTooltip = document.querySelector("[data-dashboard-refresh-tooltip]");
  const statusEl = document.querySelector("[data-dashboard-status]");
  const totalEventsEl = document.querySelector("[data-total-events]");
  const failedEventsEl = document.querySelector("[data-failed-events]");
  const lastEventEl = document.querySelector("[data-last-event]");
  const lastQueryEl = document.querySelector("[data-last-query]");
  const feedStateEl = document.querySelector("[data-dashboard-feed-state]");
  const cursorEl = document.querySelector("[data-dashboard-next-cursor]");

  const pollIntervalMs = 15000;
  const staleAfterMs = 45000;
  const maxAttempts = 3;
  let inFlight = false;
  let lastSuccessfulQueryAtMs = 0;
  let staleTimerId = 0;

  function formatLastLoadLabel(dateValue) {
    const hours24 = dateValue.getHours();
    const minutes = dateValue.getMinutes().toString().padStart(2, "0");
    const meridiem = hours24 >= 12 ? "pm" : "am";
    const hours12 = hours24 % 12 || 12;
    return `Last load ${hours12}:${minutes}${meridiem}`;
  }

  function setLastLoaded() {
    if (!refreshTooltip) {
      return;
    }

    refreshTooltip.setAttribute("data-tip", formatLastLoadLabel(new Date()));
  }

  function setStatus(value) {
    if (statusEl) {
      statusEl.textContent = value;
    }
  }

  function setFeedState(value) {
    if (feedStateEl) {
      feedStateEl.textContent = value;
    }
  }

  function toNumberText(value) {
    return Number.isFinite(value) ? String(value) : "--";
  }

  function scheduleStaleCheck() {
    if (staleTimerId) {
      window.clearTimeout(staleTimerId);
    }

    staleTimerId = window.setTimeout(function () {
      const nowMs = Date.now();
      if (!lastSuccessfulQueryAtMs || nowMs - lastSuccessfulQueryAtMs >= staleAfterMs) {
        setFeedState("Stale");
      }
    }, staleAfterMs + 50);
  }

  async function requestWithBackoff(payload) {
    let attempt = 0;
    let delayMs = 400;

    while (attempt < maxAttempts) {
      try {
        return await window.durableStackApi.request("/api/reports/dashboard-summary", {
          method: "POST",
          headers: {
            "Content-Type": "application/json"
          },
          body: JSON.stringify(payload),
          retries: 0
        });
      } catch (error) {
        attempt += 1;

        if (attempt >= maxAttempts) {
          throw error;
        }

        await new Promise(function (resolve) {
          window.setTimeout(resolve, delayMs);
        });

        delayMs = Math.min(delayMs * 2, 3200);
      }
    }

    return null;
  }

  async function fetchDashboard(isManual) {
    if (inFlight || !window.durableStackApi) {
      return;
    }

    inFlight = true;

    if (isManual) {
      setStatus("Refreshing");
    }

    try {
      const payload = {
        sinceCursor: cursorEl ? cursorEl.value || null : null
      };

      const response = await requestWithBackoff(payload);

      const data = response.data || {};

      setStatus(data.status || "Connected");
      setFeedState("Fresh");

      if (totalEventsEl) {
        totalEventsEl.textContent = toNumberText(data.totalEvents);
      }

      if (failedEventsEl) {
        failedEventsEl.textContent = toNumberText(data.failedEvents);
      }

      if (lastEventEl) {
        lastEventEl.textContent = data.lastEventAtUtc || "N/A";
      }

      if (lastQueryEl) {
        lastQueryEl.textContent = data.queryRunAtUtc || "N/A";
      }

      if (cursorEl && data.nextCursor) {
        cursorEl.value = data.nextCursor;
      }

      setLastLoaded();
      lastSuccessfulQueryAtMs = Date.now();
      scheduleStaleCheck();
    } catch (error) {
      setStatus("Unavailable");
      setFeedState("Retrying");

      if (window.durableStackToasts && isManual) {
        window.durableStackToasts.showError("Could not refresh dashboard data.", 5000);
      }
    } finally {
      inFlight = false;
    }
  }

  if (refreshTrigger) {
    refreshTrigger.addEventListener("click", function () {
      fetchDashboard(true);
      refreshTrigger.blur();
    });
  }

  fetchDashboard(false);
  scheduleStaleCheck();
  window.setInterval(function () {
    fetchDashboard(false);
  }, pollIntervalMs);
})();
