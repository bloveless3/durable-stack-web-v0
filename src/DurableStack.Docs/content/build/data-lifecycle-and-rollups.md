# Data Lifecycle, Rollups, and Retention Strategy

Defines production data lifecycle policy for telemetry storage, dashboard performance, and tenant plan-based retention.

## Goals

- Keep dashboard near-real-time for recent activity.
- Control query latency and storage cost at multi-tenant scale.
- Align retention with product tiers and pricing.

## Retention policy by tier

Recommended launch policy:

- Free tier: `7 days` telemetry retention.
- Paid tier: `2 years` telemetry retention.

Why this is a good policy:

- Free plan remains useful for immediate operational troubleshooting.
- Paid plan supports historical trend analysis, audits, and exports.
- Retention differential creates clear monetization value without changing core dashboard UX.

## Hybrid rollup model (recommended)

Use a dual-read dashboard model:

- Raw table for recent window (`now - 1h` to `now`) for near-real-time freshness.
- Rollup table(s) for older data (`<= now - 1h`) to reduce read amplification.

Dashboard query path by timeframe:

- `last_hour`: raw only.
- `last_24h`, `last_7d`, `last_30d`: raw for most recent hour + rollup for older buckets.

This gives near-live behavior while keeping larger window queries predictable.

## Rollup cadence and granularity

Proposed rollup process:

- Rollup job runs every 5 minutes.
- Aggregates finalized buckets older than 1 hour.
- Granularities:
  - `1m` (optional for advanced analytics)
  - `15m`
  - `2h`
  - `12h`

Store per-bucket aggregates for:

- run started/succeeded/failed/retried counts
- heartbeat count
- p95 duration inputs or approximate percentile sketches
- grouped failure counters (tenant + job + error signature)

## DurableStack-native background jobs

Use DurableStack itself to run lifecycle pipelines in distributed-safe mode:

- `telemetry-rollup-worker`
  - recurring every 5 minutes
  - claims finalized windows older than 1 hour
  - writes idempotent rollup rows per tenant + bucket
- `telemetry-retention-worker`
  - recurring daily (off-peak)
  - enforces tier-aware retention on raw + rollup tables
  - supports dry-run mode and emits deletion metrics

Why this is preferred:

- Uses your own distributed scheduling/claiming semantics.
- Gives operational proof that DurableStack handles real platform workflows.
- Keeps worker operations observable through the same telemetry model.

## Failure grouping strategy

For dashboard failure panels, group by:

- `tenant`
- `jobName`
- `errorType`
- normalized `errorMessage`

Return group metadata:

- `failureCount`
- `firstOccurredAtUtc`
- `lastOccurredAtUtc`
- representative worker/attempt/duration sample

This removes noisy duplicate rows and highlights top recurring issues first.

## Retention implementation guidance

- Persist tenant tier in control plane (free vs paid).
- Retention worker enforces policy daily:
  - delete/partition-prune raw telemetry older than tier retention
  - delete/prune rollups older than tier retention
- Ensure exports and report endpoints honor retention boundaries.

## Reports and exports data dependency matrix

Before pruning raw events, ensure each product surface has a durable source:

| Surface | Time horizon | Read source | Raw required? |
|---|---|---|---|
| Dashboard `last_hour` | 1 hour | Raw telemetry | Yes |
| Dashboard `last_24h/7d/30d` | 24h-30d | Hybrid (raw + rollup) | Recent hour only |
| Failure analysis report | 30d+ | Rollup + grouped failure index | Optional for deep drill-through |
| Worker health trends | 30d+ | Rollup worker metrics | No (after rollup window) |
| Job performance report | 30d+ | Rollup duration distributions | Optional for per-run forensics |
| Tenant usage report/billing | 30d+ | Rollup counters | No |
| CSV/JSON exports (free tier) | 7d | Raw + rollup | Bound by tier policy |
| CSV/JSON exports (paid tier) | up to 2y | Rollup + optional raw slice | Raw optional after rollup cutoff |

Implementation guardrails:

- Never delete raw rows newer than rollup watermark for that tenant/window.
- Track rollup watermark per tenant to guarantee export/report completeness.
- Block retention deletion for windows with failed/incomplete rollup jobs.

## Pre-launch checklist

1. Implement retention worker with dry-run + metrics mode.
2. Add dashboard query telemetry (latency, scanned rows, cache hit).
3. Add load tests for `last_24h`, `last_7d`, `last_30d` at projected tenant counts.
4. Validate tier migration behavior (free -> paid, paid -> free).
5. Publish retention terms in product docs and pricing pages.
