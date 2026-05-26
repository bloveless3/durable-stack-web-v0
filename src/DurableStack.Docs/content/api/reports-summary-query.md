# POST /v1/reports/summary/query

Returns a user-scoped telemetry summary for selected global filters.

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
  "fromUtc": "2026-05-25T00:00:00Z",
  "toUtc": "2026-05-25T23:59:59Z",
  "sinceCursor": "MTc0ODIwMDAwMDAwMA=="
}
```

## Filter semantics

- Requested filter IDs are intersected with the caller's authorized scope.
- If no IDs are provided for a dimension, all authorized IDs are used.
- If intersection results in no tenant IDs, response is `200 OK` with zero totals.

## Validation constraints

- Each ID list can contain at most 100 values.
- `fromUtc` must be less than or equal to `toUtc`.
- Requested time window must be 90 days or less.
- `sinceCursor` must be a valid cursor from a prior response.

## Success response

```json
{
  "tenantId": null,
  "totalEvents": 42,
  "failedEvents": 3,
  "lastEventAtUtc": "2026-05-25T21:14:13.870Z",
  "scopeTenantIds": [
    "7fd34690-0f65-4e85-a588-7bc4fe95ed4d"
  ],
  "windowStartUtc": "2026-05-25T00:00:00Z",
  "windowEndUtc": "2026-05-25T23:59:59Z",
  "queryRunAtUtc": "2026-05-25T22:01:03.110Z",
  "nextCursor": "MTc0ODIxMDQ2MzExMA=="
}
```

## Incremental refresh workflow

1. Initial load without `sinceCursor`.
2. Store response `nextCursor`.
3. Poll using `sinceCursor` from prior response.
4. Replace dashboard cards with authoritative response values.

## Error model

Problem Details is returned for invalid tokens and invalid query payloads.

See:

- `/problems/auth-failed`
- `/problems/validation-failed`
