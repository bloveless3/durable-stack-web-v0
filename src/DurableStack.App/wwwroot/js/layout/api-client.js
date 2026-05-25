(function () {
  async function sleep(milliseconds) {
    return await new Promise(function (resolve) {
      window.setTimeout(resolve, milliseconds);
    });
  }

  function parseJsonSafely(text) {
    if (!text) {
      return null;
    }

    try {
      return JSON.parse(text);
    } catch {
      return null;
    }
  }

  function createError(status, message, details) {
    const error = new Error(message || `Request failed (${status})`);
    error.status = status;
    error.details = details;
    return error;
  }

  async function request(url, options) {
    const requestOptions = options || {};
    const retries = Number.isFinite(requestOptions.retries) ? requestOptions.retries : 0;
    const retryDelayMs = Number.isFinite(requestOptions.retryDelayMs) ? requestOptions.retryDelayMs : 250;
    const retryStatuses = Array.isArray(requestOptions.retryStatuses) ? requestOptions.retryStatuses : [408, 429, 500, 502, 503, 504];

    let attempt = 0;
    while (true) {
      try {
        const response = await fetch(url, {
          method: requestOptions.method || "GET",
          headers: requestOptions.headers,
          body: requestOptions.body,
          signal: requestOptions.signal
        });

        const responseText = await response.text();
        const payload = parseJsonSafely(responseText);

        if (!response.ok) {
          const shouldRetry = attempt < retries && retryStatuses.includes(response.status);
          if (shouldRetry) {
            attempt += 1;
            await sleep(retryDelayMs * attempt);
            continue;
          }

          throw createError(response.status, payload?.error || response.statusText, payload);
        }

        return {
          status: response.status,
          ok: true,
          data: payload,
          text: responseText,
          headers: response.headers
        };
      } catch (error) {
        const isAbort = error && error.name === "AbortError";
        if (isAbort) {
          throw error;
        }

        const shouldRetry = attempt < retries;
        if (shouldRetry) {
          attempt += 1;
          await sleep(retryDelayMs * attempt);
          continue;
        }

        throw error;
      }
    }
  }

  window.durableStackApi = {
    request: request
  };
})();
