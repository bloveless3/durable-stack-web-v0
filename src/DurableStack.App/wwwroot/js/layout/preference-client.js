(function () {
  async function requestJson(url, options) {
    const response = await fetch(url, options);
    if (!response.ok) {
      throw new Error(`Preference request failed (${response.status})`);
    }

    const contentType = response.headers.get("content-type") || "";
    if (contentType.includes("application/json")) {
      return await response.json();
    }

    return null;
  }

  async function setPreference(key, value) {
    await requestJson("/api/preferences", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ key: key, value: value })
    });
  }

  async function getPreferences(keys) {
    if (!Array.isArray(keys) || keys.length === 0) {
      return {};
    }

    const query = keys.map(function (key) {
      return `key=${encodeURIComponent(key)}`;
    }).join("&");

    return await requestJson(`/api/preferences?${query}`, {
      method: "GET"
    }) || {};
  }

  window.durableStackPreferences = {
    set: setPreference,
    getMany: getPreferences
  };
})();
