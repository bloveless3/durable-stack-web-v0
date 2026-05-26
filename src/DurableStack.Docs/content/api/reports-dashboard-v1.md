# Dashboard V1 Information Architecture

Defines the first dashboard layout, widget behavior, and metric formulas for tenant telemetry.

This page is a product and API planning spec. Endpoint-level request/response contracts are defined in `/api/reports-dashboard-query.md`.

## Global filters

Dashboard views are scoped by the same authorization-aware global filters used by reports:

- `organizationIds`
- `projectIds`
- `tenantIds`
- `timeframe`

Supported `timeframe` values:

- `last_hour`
- `last_24h`
- `last_7d`
- `last_30d`

## Time bucket strategy

The main chart auto-selects bucket size based on timeframe to keep chart density readable.

| Timeframe | Bucket size | Approx. points |
|---|---|---|
| `last_hour` | `1 minute` | 60 |
| `last_24h` | `15 minutes` | 96 |
| `last_7d` | `2 hours` | 84 |
| `last_30d` | `12 hours` | 60 |

Rules:

- Buckets are UTC-aligned.
- Empty buckets are returned with zero counts.
- `windowStartUtc` is inclusive; `windowEndUtc` is exclusive.
- The same selected bucket size is used across all main-chart series.

## Layout

## Top KPI row

1. `Runs` (total terminal runs)
2. `Success rate`
3. `Failure rate`
4. `Retry rate`
5. `Active workers`
6. `P95 duration (ms)`

## Main panel (left 2/3)

Combo chart over selected timeframe:

- Line: `runStarted`
- Line: `runSucceeded`
- Line: `runFailed`
- Line: `runRetried`
- Area: `heartbeatCount` (secondary axis)

## Worker panel (right 1/3)

Worker cards grouped by current status:

- Status dot (`online`, `warn`, `offline`)
- `workerName`
- `tenantDisplayName` (`<ProjectName> - <EnvironmentName>`)
- `lastSeenAtUtc`
- `freshnessSeconds`
- `heartbeatsPerMinute` (rolling over selected timeframe)
- `lastJobName`
- `lastJobOutcome`
- `successRate`

Status thresholds (initial default):

- `online`: `freshnessSeconds <= 20`
- `warn`: `20 < freshnessSeconds <= 60`
- `offline`: `freshnessSeconds > 60`

## Bottom panel

Grouped failures table:

- `failureCount`
- `tenantDisplayName`
- `jobName`
- `errorType`
- `errorMessage`
- `lastOccurredAtUtc`

When detailed error sync is disabled for a tenant, grouped failures will display this note:

- `Detailed error message collection is disabled for this tenant.`

In that mode, message values may show as `N/A` while error type and counts remain available.

To enable message-level details, set `DetailedErrorSyncEnabled = true` for the tenant in the control-plane data.

Grouping key:

- tenant + job + error type + error message

## Metric definitions

- `runStarted`: count of `eventType = job_started`
- `runSucceeded`: count of `eventType = job_succeeded`
- `runFailed`: count of `eventType = job_failed`
- `runRetried`: count of `eventType = job_retried`
- `heartbeatCount`: sum of `payloadJson.heartbeatCount` where `eventType = worker_heartbeat_batch`
- `runsTotal`: `runSucceeded + runFailed`
- `successRate`: `runSucceeded / max(runsTotal, 1)`
- `failureRate`: `runFailed / max(runsTotal, 1)`
- `retryRate`: `runRetried / max(runStarted, 1)` (used for backend summary calculations)
- `p95DurationMs`: percentile 95 over terminal event `durationMs` (`job_succeeded`, `job_failed`)

## Data quality and null handling

- Missing `durationMs` values are excluded from percentile calculations.
- Workers with no heartbeat in the selected window are excluded from active counts.
- `heartbeatCount` falls back to `0` when payload field is absent or invalid.
- If `eventType` is unknown, the event is ignored for v1 aggregates.

## Output consistency requirements

- Dashboard response includes `queryRunAtUtc` and is authoritative for the selected timeframe/filter window.
- Widget totals and chart series are computed from the same filtered dataset.
- All timestamps are emitted in UTC ISO-8601 format.
- App refresh should replace UI with full-window response values, not apply incremental delta merges.

## Performance notes for scale

To support high tenant volume and large telemetry growth, v1 dashboard query design assumes:

- Time-window bounded queries only (`last_hour`, `last_24h`, `last_7d`, `last_30d`).
- Pre-extracted `heartbeatCount` column for `worker_heartbeat_batch` events to avoid runtime JSON parsing in query hot paths.
- Covering indexes for common query dimensions:
  - `telemetry_events(event_type, occurred_at_utc)`
  - `telemetry_events(occurred_at_utc, batch_id)`
  - `telemetry_events(worker_name, occurred_at_utc)`
- Strictly capped grouped failure table size per request (`TOP 50` / `LIMIT 50`).

Current implementation notes:

1. Dashboard query can run in hybrid mode behind feature flag (`TelemetryLifecycle:Query:EnableHybridRollupReads`):
   - raw for most recent aligned hour window
   - rollups for older buckets
2. If historical raw data exists but rollups are not available for that historical segment, query falls back to full-window raw reads to avoid partial/missing results.

Recommended next optimizations after v1:

1. Add table partitioning by time (monthly/daily depending on ingest volume).
2. Consider background worker status snapshots for instant right-panel load.
3. Add query-level cache keyed by `(scope hash + timeframe)` with short TTL (15-30s).

## Next Steps

1. Add tenant-scale load testing for `last_hour` and `last_24h` dashboard query latency and p95/p99 tracking.
2. Add backend short-lived cache for identical dashboard queries (scope + timeframe) with 15-30 second TTL.
3. Add dashboard integration tests for stale-state and no-data rendering paths in the App.
4. Wire `ExecutionMode=durablestack` once package integration is available.
