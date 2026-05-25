# POST /v1/events/batch

Ingests a telemetry batch for a tenant.

## Authentication

Provide both headers:

- `X-DurableStack-TenantId`
- `X-DurableStack-ClientSecret`

Optional tracing header:

- `X-Correlation-Id`

## Request body

```json
{
  "tenantId": "tenant_1234567890abcdef",
  "idempotencyKey": "tool-batch-2026-05-25T20:15:00Z-001",
  "serviceName": "durable-worker",
  "environmentName": "Production",
  "events": [
    {
      "eventType": "job_completed",
      "eventVersion": 1,
      "occurredAtUtc": "2026-05-25T20:14:59.123Z",
      "runId": "8e5f57a2-ec86-42d9-8362-eb71044537f9",
      "jobName": "NightlySync",
      "attempt": 1,
      "workerName": "worker-a",
      "durationMs": 123.45,
      "payloadJson": "{\"result\":\"ok\"}"
    }
  ]
}
```

## Validation rules

- `events` is required and must have at least 1 event.
- `events` must not exceed tenant `MaxBatchSize`.
- `tenantId` is optional, but if provided it must match `X-DurableStack-TenantId`.
- `idempotencyKey` max length: 200.
- `serviceName` max length: 200.
- `environmentName` max length: 100.
- Per event:
  - `eventType` required, max 100.
  - `eventVersion` must be greater than 0.
  - `occurredAtUtc` must be within 30 days in the past and 10 minutes in the future.
  - `attempt` cannot be negative.
  - `durationMs` cannot be negative.
  - `jobName` max 200.
  - `workerName` max 200.
  - `errorType` max 200.
  - `errorMessage` max 4000.
  - `payloadJson` max length: 65535 and must be valid JSON when present.

## Success response

```json
{
  "acceptedCount": 1,
  "rejectedCount": 0,
  "serverTimeUtc": "2026-05-25T20:15:00.456Z",
  "idempotencyKey": "tool-batch-2026-05-25T20:15:00Z-001",
  "isDuplicate": false,
  "correlationId": "tool-corr-abc-123"
}
```

If the same `idempotencyKey` is replayed for the same tenant, the API returns
`200 OK` with `isDuplicate: true` and the original accepted/rejected counts.

## Error model

Validation and auth issues are returned as Problem Details.

See:

- `/problems/auth-failed`
- `/problems/payload-too-large`
- `/problems/sync-disabled`
- `/problems/validation-failed`
