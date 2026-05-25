(function () {
  if (!window.durableStackApi) {
    return;
  }

  async function setPreference(key, value) {
    await window.durableStackApi.request("/api/preferences", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ key: key, value: value }),
      retries: 1
    });
  }

  async function getPreferences(keys) {
    if (!Array.isArray(keys) || keys.length === 0) {
      return {};
    }

    const query = keys.map(function (key) {
      return `key=${encodeURIComponent(key)}`;
    }).join("&");

    const response = await window.durableStackApi.request(`/api/preferences?${query}`, {
      method: "GET",
      retries: 1
    });

    return response.data || {};
  }

  async function getPreference(key) {
    const values = await getPreferences([key]);
    return values[key] || null;
  }

  window.durableStackPreferences = {
    set: setPreference,
    get: getPreference,
    getMany: getPreferences
  };
})();
