# App Build Guide

This guide describes the current App-side architecture and requirements for BFF integration.

## Current state

- App no longer stores tenant API credentials in appsettings.
- App still stores API base URL in config:
  - `DurableStackApi:BaseUrl`
- Dashboard report cards are placeholder-based until BFF report endpoints are wired.
- Global filters are user-scoped and include:
  - organization
  - project
  - tenant
  - time range

## BFF integration requirements

- App should call API with user bearer tokens, not tenant client secrets.
- App must pass selected global filters as report query payload.
- App should support incremental polling via cursor:
  - send `sinceCursor` from prior response
  - use `queryRunAtUtc` + `nextCursor` from API response

## Phase 3 implementation status

- App issues short-lived user JWTs server-side for `reports.read`.
- App uses BFF endpoint `POST /api/reports/dashboard-summary` to query API.
- Dashboard auto-refresh polls every 15 seconds and forwards `sinceCursor` incrementally.
- Dashboard refresh button performs an immediate manual query.
- Correlation IDs are forwarded from App to API per request.

## Configuration

```json
{
  "DurableStackApi": {
    "BaseUrl": "https://localhost:7163"
  }
}
```

## Local build

From repository root:

```bash
dotnet build src/DurableStack.App/DurableStack.App.csproj
```

The App build runs Tailwind asset compilation as part of MSBuild.
