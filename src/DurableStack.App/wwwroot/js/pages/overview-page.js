(function () {
  const refreshTrigger = document.querySelector("[data-dashboard-refresh]");
  const refreshTooltip = document.querySelector("[data-dashboard-refresh-tooltip]");
  const statusEl = document.querySelector("[data-dashboard-status]");
  const runsTotalEl = document.querySelector("[data-kpi-runs-total]");
  const successRateEl = document.querySelector("[data-kpi-success-rate]");
  const failureRateEl = document.querySelector("[data-kpi-failure-rate]");
  const retryRateEl = document.querySelector("[data-kpi-retry-rate]");
  const activeWorkersEl = document.querySelector("[data-kpi-active-workers]");
  const p95DurationEl = document.querySelector("[data-kpi-p95-duration]");
  const timeframeEl = document.querySelector("[data-dashboard-timeframe]");
  const lastEventEl = document.querySelector("[data-last-event]");
  const lastQueryEl = document.querySelector("[data-last-query]");
  const feedStateEl = document.querySelector("[data-dashboard-feed-state]");
  const chartEl = document.querySelector("[data-runs-chart]");
  const chartEmptyEl = document.querySelector("[data-runs-chart-empty]");
  const workerOnlineCountEl = document.querySelector("[data-worker-online-count]");
  const workerWarnCountEl = document.querySelector("[data-worker-warn-count]");
  const workerOfflineCountEl = document.querySelector("[data-worker-offline-count]");
  const workerListEl = document.querySelector("[data-worker-list]");
  const failuresBodyEl = document.querySelector("[data-failures-table-body]");

  const pollIntervalMs = 15000;
  const staleAfterMs = 45000;
  const maxAttempts = 3;
  let inFlight = false;
  let lastSuccessfulQueryAtMs = 0;
  let staleTimerId = 0;
  let googleChartsReady = false;
  const expandedWorkers = new Set();

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

  function toText(value, fallback) {
    if (value === undefined || value === null || value === "") {
      return fallback;
    }

    return String(value);
  }

  function timeframeLabel(timeframe, bucketSize) {
    const base = {
      last_hour: "Last hour",
      last_24h: "Last 24h",
      last_7d: "Last 7 days",
      last_30d: "Last 30 days"
    }[timeframe] || "Selected range";

    return `${base} (${bucketSize || "--"} buckets)`;
  }

  function statusClass(status) {
    if (status === "online") {
      return "worker-online";
    }

    if (status === "warn") {
      return "worker-warn";
    }

    return "worker-offline";
  }

  function formatWorkerLastSeen(utcText) {
    if (!utcText || utcText === "N/A") {
      return "N/A";
    }

    const parsed = new Date(utcText);
    if (Number.isNaN(parsed.getTime())) {
      return utcText;
    }

    const parts = new Intl.DateTimeFormat("en-US", {
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
      hour12: true,
      timeZoneName: "short"
    }).formatToParts(parsed);

    const month = parts.find(function (x) { return x.type === "month"; })?.value || "";
    const day = parts.find(function (x) { return x.type === "day"; })?.value || "";
    const hour = parts.find(function (x) { return x.type === "hour"; })?.value || "";
    const minute = parts.find(function (x) { return x.type === "minute"; })?.value || "";
    const second = parts.find(function (x) { return x.type === "second"; })?.value || "";
    const dayPeriod = parts.find(function (x) { return x.type === "dayPeriod"; })?.value || "";
    const zone = parts.find(function (x) { return x.type === "timeZoneName"; })?.value || "";

    return `${month}-${day} ${hour}:${minute}:${second} ${dayPeriod} ${zone}`.trim();
  }

  function workerIdentity(item) {
    return `${toText(item.tenantDisplayName, "N/A")}|${toText(item.workerName, "(unknown)")}`;
  }

  function renderWorkers(workers) {
    if (!workerOnlineCountEl || !workerWarnCountEl || !workerOfflineCountEl || !workerListEl) {
      return;
    }

    const counts = workers && workers.statusCounts ? workers.statusCounts : { online: 0, warn: 0, offline: 0 };
    workerOnlineCountEl.textContent = toNumberText(counts.online);
    workerWarnCountEl.textContent = toNumberText(counts.warn);
    workerOfflineCountEl.textContent = toNumberText(counts.offline);

    const items = workers && Array.isArray(workers.items) ? workers.items : [];
    if (items.length === 0) {
      workerListEl.innerHTML = '<p class="dashboard-empty-row">No workers reported in this window.</p>';
      return;
    }

    workerListEl.innerHTML = items.map(function (item, index) {
      const workerId = `worker-${index}-${toText(item.workerName, "unknown").replace(/[^a-zA-Z0-9_-]/g, "-")}`;
      const identity = workerIdentity(item);
      const isExpanded = expandedWorkers.has(identity);
      return `
        <article class="worker-card ${statusClass(item.status)}">
          <button type="button" class="worker-card-toggle" data-worker-toggle data-worker-id="${escapeHtml(identity)}" aria-expanded="${isExpanded ? "true" : "false"}" aria-controls="${workerId}">
            <span class="worker-heading">
              <strong>${toText(item.workerName, "(unknown)")}</strong>
              <em>${toText(item.tenantDisplayName, "N/A")}</em>
            </span>
            <span class="worker-state">${toText(item.status, "offline")}</span>
          </button>
          <div id="${workerId}" class="worker-card-body" ${isExpanded ? "" : "hidden"}>
            <dl>
              <div><dt>Last seen</dt><dd>${formatWorkerLastSeen(toText(item.lastSeenAtUtc, "N/A"))}</dd></div>
              <div><dt>Freshness</dt><dd>${toText(item.freshnessSeconds, 0)}s</dd></div>
              <div><dt>HB/min</dt><dd>${toText(item.heartbeatsPerMinute, "0.0")}</dd></div>
              <div><dt>Last job</dt><dd>${toText(item.lastJobName, "N/A")}</dd></div>
              <div><dt>Outcome</dt><dd>${toText(item.lastJobOutcome, "N/A")}</dd></div>
              <div><dt>Success</dt><dd>${toText(item.successRate, "0.0%")}</dd></div>
            </dl>
          </div>
        </article>
      `;
    }).join("");
  }

  function escapeHtml(value) {
    return String(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  }

  function renderFailures(failures) {
    if (!failuresBodyEl) {
      return;
    }

    const rows = Array.isArray(failures) ? failures : [];
    if (rows.length === 0) {
      failuresBodyEl.innerHTML = '<tr><td colspan="6" class="dashboard-empty-row">No failures in this window.</td></tr>';
      return;
    }

    failuresBodyEl.innerHTML = rows.map(function (item) {
      return `
        <tr>
          <td>${escapeHtml(toText(item.occurredAtUtc, "N/A"))}</td>
          <td>${escapeHtml(toText(item.jobName, "(unknown)"))}</td>
          <td>${escapeHtml(toText(item.workerName, "(unknown)"))}</td>
          <td>${escapeHtml(toText(item.errorType, "N/A"))}</td>
          <td title="${escapeHtml(toText(item.errorMessage, "N/A"))}">${escapeHtml(toText(item.errorMessage, "N/A"))}</td>
          <td>${escapeHtml(toText(item.runId, "N/A"))}</td>
        </tr>
      `;
    }).join("");
  }

  function renderChart(series) {
    if (!chartEl || !chartEmptyEl) {
      return;
    }

    const points = Array.isArray(series) ? series : [];
    const hasData = points.some(function (x) {
      return (x.runStarted || 0) > 0 ||
        (x.runSucceeded || 0) > 0 ||
        (x.runFailed || 0) > 0 ||
        (x.runRetried || 0) > 0 ||
        (x.heartbeatCount || 0) > 0;
    });

    if (points.length === 0 || !hasData) {
      chartEmptyEl.hidden = false;
      if (googleChartsReady && window.google && window.google.visualization) {
        chartEl.innerHTML = "";
      }
      return;
    }

    if (!googleChartsReady || !window.google || !window.google.visualization) {
      return;
    }

    const dataTable = new window.google.visualization.DataTable();
    dataTable.addColumn("datetime", "Time");
    dataTable.addColumn("number", "Started");
    dataTable.addColumn("number", "Succeeded");
    dataTable.addColumn("number", "Failed");
    dataTable.addColumn("number", "Retried");
    dataTable.addColumn("number", "Heartbeats");

    points.forEach(function (point) {
      dataTable.addRow([
        new Date(point.bucketStartUtc),
        point.runStarted || 0,
        point.runSucceeded || 0,
        point.runFailed || 0,
        point.runRetried || 0,
        point.heartbeatCount || 0
      ]);
    });

    const options = {
      chartArea: {
        left: 50,
        right: 24,
        top: 14,
        bottom: 36,
        width: "100%",
        height: "82%"
      },
      legend: {
        position: "none"
      },
      backgroundColor: "transparent",
      hAxis: {
        textStyle: { color: "#6b756f", fontSize: 11 },
        format: "h:mm a",
        gridlines: { color: "#e5ece7" },
        baselineColor: "#d8e0da",
        slantedText: false,
        maxAlternation: 1
      },
      vAxis: {
        viewWindow: { min: 0 },
        textStyle: { color: "#6b756f", fontSize: 11 },
        gridlines: { color: "#e5ece7" },
        baselineColor: "#d8e0da"
      },
      series: {
        0: { color: "#2563eb", lineWidth: 2 },
        1: { color: "#16a34a", lineWidth: 2 },
        2: { color: "#dc2626", lineWidth: 2 },
        3: { color: "#d97706", lineWidth: 2 },
        4: { color: "#7c3aed", lineWidth: 2, targetAxisIndex: 1 }
      },
      vAxes: {
        0: {
          viewWindow: { min: 0 },
          textStyle: { color: "#6b756f", fontSize: 11 },
          gridlines: { color: "#e5ece7" },
          baselineColor: "#d8e0da"
        },
        1: {
          viewWindow: { min: 0 },
          textStyle: { color: "#6b756f", fontSize: 11 },
          gridlines: { color: "transparent" },
          baselineColor: "#d8e0da"
        }
      },
      pointSize: 0,
      lineWidth: 2,
      enableInteractivity: true,
      tooltip: { trigger: "focus" },
      crosshair: {
        color: "#9ca8a1",
        trigger: "focus",
        focused: { opacity: 0.6 }
      }
    };

    if (points.length <= 24) {
      options.pointSize = 2;
    }

    const chart = new window.google.visualization.LineChart(chartEl);
    chart.draw(dataTable, options);

    chartEmptyEl.hidden = true;
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

  async function requestWithBackoff() {
    let attempt = 0;
    let delayMs = 400;

    while (attempt < maxAttempts) {
      try {
        return await window.durableStackApi.request("/api/reports/dashboard", {
          method: "GET",
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
      const response = await requestWithBackoff();

      const data = response.data || {};

      setStatus(data.status || "Connected");
      setFeedState("Fresh");

      const summary = data.summary || {};

      if (runsTotalEl) {
        runsTotalEl.textContent = toNumberText(summary.runsTotal);
      }

      if (successRateEl) {
        successRateEl.textContent = toText(summary.successRate, "--");
      }

      if (failureRateEl) {
        failureRateEl.textContent = toText(summary.failureRate, "--");
      }

      if (retryRateEl) {
        retryRateEl.textContent = toText(summary.retryRate, "--");
      }

      if (activeWorkersEl) {
        activeWorkersEl.textContent = toNumberText(summary.activeWorkers);
      }

      if (p95DurationEl) {
        p95DurationEl.textContent = toText(summary.p95DurationMs, "N/A");
      }

      if (timeframeEl) {
        timeframeEl.textContent = timeframeLabel(data.timeframe, data.bucketSize);
      }

      if (lastEventEl) {
        lastEventEl.textContent = toText(data.lastEventAtUtc, "N/A");
      }

      if (lastQueryEl) {
        lastQueryEl.textContent = toText(data.queryRunAtUtc, "N/A");
      }

      window.__durableStackLastSeries = data.series || [];
      renderChart(window.__durableStackLastSeries);
      renderWorkers(data.workers || null);
      renderFailures(data.recentFailures || []);

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

  document.addEventListener("durablestack:filters-changed", function () {
    fetchDashboard(false);
  });

  if (refreshTrigger) {
    refreshTrigger.addEventListener("click", function () {
      fetchDashboard(true);
      refreshTrigger.blur();
    });
  }

  if (workerListEl) {
    workerListEl.addEventListener("click", function (event) {
      const target = event.target;
      if (!(target instanceof Element)) {
        return;
      }

      const toggle = target.closest("[data-worker-toggle]");
      if (!toggle) {
        return;
      }

      const isExpanded = toggle.getAttribute("aria-expanded") === "true";
      const workerId = toggle.getAttribute("data-worker-id");
      const contentId = toggle.getAttribute("aria-controls");
      const content = contentId ? document.getElementById(contentId) : null;
      if (!content) {
        return;
      }

      toggle.setAttribute("aria-expanded", isExpanded ? "false" : "true");
      content.hidden = isExpanded;

      if (workerId) {
        if (isExpanded) {
          expandedWorkers.delete(workerId);
        } else {
          expandedWorkers.add(workerId);
        }
      }
    });
  }

  fetchDashboard(false);
  scheduleStaleCheck();
  window.setInterval(function () {
    fetchDashboard(false);
  }, pollIntervalMs);

  if (window.google && window.google.charts) {
    window.google.charts.load("current", { packages: ["corechart"] });
    window.google.charts.setOnLoadCallback(function () {
      googleChartsReady = true;
      fetchDashboard(false);
      let resizeTimer = 0;
      window.addEventListener("resize", function () {
        if (resizeTimer) {
          window.clearTimeout(resizeTimer);
        }

        resizeTimer = window.setTimeout(function () {
          renderChart(window.__durableStackLastSeries || []);
        }, 160);
      });
    });
  }
})();
