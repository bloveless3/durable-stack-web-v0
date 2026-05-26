# API Build Guide

This guide documents current API auth lanes and reporting requirements.

## Auth lanes

- Ingestion lane (`/v1/events/*`):
  - `X-DurableStack-TenantId`
  - `X-DurableStack-ClientSecret`
- User reporting lane (`/v1/reports/*`):
  - JWT bearer token
  - `reports.read` scope required

## User JWT configuration

```json
{
  "Authentication": {
    "UserJwt": {
      "Issuer": "DurableStack.App",
      "Audience": "DurableStack.Api",
      "SigningKey": "dev-only-signing-key-change-me-please-32chars",
      "RequireHttpsMetadata": false
    }
  }
}
```

Use secret storage for signing keys outside local development.

## Reporting endpoint

- `POST /v1/reports/dashboard/query`
- User-scope is enforced by membership intersection.
- Timeframe and filter limits are validated server-side.
- Response is authoritative for the selected global filter window.

## App BFF caller expectations

- App sends user bearer token with `reports.read` scope.
- App forwards correlation ID (`X-Correlation-Id`) for observability.
- App polls full-window dashboard query for near-real-time freshness.

## Local build and tests

From repository root:

```bash
dotnet test DurableStack.Platform.sln
```
