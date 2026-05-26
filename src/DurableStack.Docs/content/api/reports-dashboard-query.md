# POST /v1/reports/dashboard/query

Returns all dashboard v1 data in a single user-scoped response.

## Authentication

Requires bearer token authentication.

- `Authorization: Bearer <user-access-token>`

The token must include `reports.read` in the `scope` claim.

## Request body

```json
{
  "organizationIds": [
    "4afab794-7054-4865-86c7-153f8d64f0f2"
  ],
  "projectIds": [
    "37968fbe-6f13-45a8-a87d-b07fd3f1774f"
  ],
  "tenantIds": [
    "7fd34690-0f65-4e85-a588-7bc4fe95ed4d"
  ],
  "timeframe": "last_24h"
}
```

## Request contract

- `organizationIds`: optional, max 100.
- `projectIds`: optional, max 100.
- `tenantIds`: optional, max 100.
- `timeframe`: required enum:
  - `last_hour`
  - `last_24h`
  - `last_7d`
  - `last_30d`

This endpoint intentionally does not support incremental cursor polling.
Each query returns authoritative results for the selected timeframe window.

## Filter semantics

- Requested filter IDs are intersected with caller authorization scope.
- If no IDs are provided for a dimension, all authorized IDs are used.
- If the resulting tenant scope is empty, response is `200 OK` with zero totals and empty series.

## Aggregation rules

Timeframe-to-bucket mapping:

| Timeframe | `bucketSize` |
|---|---|
| `last_hour` | `1m` |
| `last_24h` | `15m` |
| `last_7d` | `2h` |
| `last_30d` | `12h` |

Workers are classified using freshness at query time (`queryRunAtUtc`):

- `online`: `freshnessSeconds <= 20`
- `warn`: `20 < freshnessSeconds <= 60`
- `offline`: `freshnessSeconds > 60`

## Success response

```json
{
  "scopeTenantIds": [
    "7fd34690-0f65-4e85-a588-7bc4fe95ed4d"
  ],
  "timeframe": "last_24h",
  "windowStartUtc": "2026-05-25T00:00:00Z",
  "windowEndUtc": "2026-05-26T00:00:00Z",
  "bucketSize": "15m",
  "queryRunAtUtc": "2026-05-26T00:00:05Z",
  "summary": {
    "runStarted": 1220,
    "runSucceeded": 1180,
    "runFailed": 40,
    "runRetried": 55,
    "runsTotal": 1220,
    "successRate": 0.9672,
    "failureRate": 0.0328,
    "retryRate": 0.0451,
    "heartbeatCount": 25730,
    "activeWorkers": 9,
    "p95DurationMs": 286.4,
    "lastEventAtUtc": "2026-05-25T23:59:58.110Z"
  },
  "series": [
    {
      "bucketStartUtc": "2026-05-25T00:00:00Z",
      "runStarted": 12,
      "runSucceeded": 12,
      "runFailed": 0,
      "runRetried": 1,
      "heartbeatCount": 270
    }
  ],
  "workers": {
    "statusCounts": {
      "online": 7,
      "warn": 1,
      "offline": 1
    },
    "items": [
      {
        "workerName": "worker-a",
        "tenantDisplayName": "Demo Project - Production",
        "status": "online",
        "lastSeenAtUtc": "2026-05-25T23:59:56.880Z",
        "freshnessSeconds": 4,
        "heartbeatsPerMinute": 118.3,
        "lastJobName": "nightly-sync",
        "lastJobOutcome": "succeeded",
        "successRate": 0.9921,
        "runStarted": 248,
        "runSucceeded": 246,
        "runFailed": 2,
        "runRetried": 5,
        "p95DurationMs": 301.6
      }
    ]
  },
  "recentFailures": [
    {
      "occurredAtUtc": "2026-05-25T23:58:44.210Z",
      "jobName": "nightly-sync",
      "workerName": "worker-b",
      "runId": "98338e3f-66d8-45f0-ac31-c8c2a6e603f3",
      "attempt": 2,
      "errorType": "TimeoutException",
      "errorMessage": "Execution timed out after 30s",
      "durationMs": 30000
    }
  ]
}
```

## Response contract notes

- `summary` powers top KPI cards.
- `series` powers main time-series chart.
- `workers` powers right panel worker health cards.
- `recentFailures` powers bottom table and is sorted descending by `occurredAtUtc`.
- `series` must include zero-value buckets to render continuous charts.
- Response values are authoritative for the selected timeframe window (not incremental delta values).

## Validation constraints

- ID lists max 100 entries each.
- `timeframe` is required and must be one of the supported enum values.

## Error model

Problem Details is returned for auth and payload validation errors.

See:

- `/problems/auth-failed`
- `/problems/validation-failed`
