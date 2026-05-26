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
- App polls full-window dashboard query every 15 seconds to keep UI authoritative.

## Phase 3 implementation status

- App issues short-lived user JWTs server-side for `reports.read`.
- App uses BFF endpoint `GET /api/reports/dashboard` to query API.
- Dashboard auto-refresh polls every 15 seconds using full-window authoritative refresh.
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
