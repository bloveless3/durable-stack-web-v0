# DurableStack Platform (Web v0)

Initial solution scaffolding for hosted DurableStack platform services.

## Projects

- `src/DurableStack.Api` - ingestion and tenant-facing API (`api.durablestack.com`)
- `src/DurableStack.App` - customer portal (`app.durablestack.com`)
- `src/DurableStack.Marketing` - marketing/docs site (`durablestack.com`)
- `src/DurableStack.ControlPlane` - EF Core model + DbContext for control-plane data
- `src/DurableStack.Telemetry` - EF Core model + DbContext for telemetry data
- `src/DurableStack.Platform.Contracts` - shared DTO contracts

## Database strategy

- PostgreSQL with two databases:
  - `durablestack_control` (auth/org/project/tenant/billing/alerts config)
  - `durablestack_telemetry` (raw events, ingestion batches, reporting aggregates)
- Both are accessed with Entity Framework Core + Npgsql.

## Current status

- Solution scaffolded and builds successfully on .NET 9.
- Control-plane model now includes user/team hierarchy:
  - `User` = human account
  - `Organization` = team/company
  - `OrganizationMember` = user membership/role in organization
  - `Project` = logical application under organization
  - `Tenant` = project environment credential target (currently includes `EnvironmentName`)
- API foundation in place with:
  - health/version endpoints
  - local tenant bootstrap endpoint for development
  - authenticated telemetry ingestion (`POST /v1/events/batch`)
  - idempotency key support on ingestion
  - tenant policy enforcement at ingestion (batch limits, sync enabled/disabled, error detail redaction)
  - initial reporting summary endpoint for app integration (`GET /v1/reports/summary`)
- EF Core initial migrations generated for both DbContexts:
  - `src/DurableStack.ControlPlane/Migrations/ControlPlane`
  - `src/DurableStack.Telemetry/Migrations/Telemetry`
- App foundation includes typed API client and a basic dashboard check wired to API summary.
- App shell now includes a static header, workspace sidebar, and Ctrl+K command palette UX baseline.
- Styling now uses real Tailwind CSS with SCSS entry source (`src/DurableStack.App/wwwroot/scss/app.scss`) and compiled output (`src/DurableStack.App/wwwroot/scss/app.css`).
- App auth foundation now includes ASP.NET Core Identity + cookie auth with an email-first flow (`/auth` -> `/auth/sign-in` or `/register`).
- Identity policy currently enforces 12+ character passwords with non-alphanumeric requirement and account lockout (5 failed attempts, 15-minute lockout).
- App Identity EF migration has been scaffolded under `src/DurableStack.App/Data/Migrations/Identity`.
- Tenant options are currently treated as ingestion-time server policy only; workers do not fetch runtime options.
- Design-time EF DbContext factories now resolve connection strings from project appsettings files first (`appsettings.json` + `appsettings.Development.json`) with env var fallback.
- Development startup now auto-applies EF migrations for API/App data stores.

## UX notes

- Page-specific scripts are loaded from dedicated files under `src/DurableStack.App/wwwroot/js/pages/`.
- Layout-level interactions live under `src/DurableStack.App/wwwroot/js/layout/`.
- Tailwind/SCSS compilation is wired for Visual Studio via `compilerconfig.json` and also runs via MSBuild pre-build in `DurableStack.App.csproj`.
- Auth has a separate layout and stylesheet (`src/DurableStack.App/Views/Shared/_AuthLayout.cshtml`, `src/DurableStack.App/wwwroot/scss/auth.scss`).
- App includes a reusable `ICurrentUserContext` service for accessing the authenticated user from application code.
- Marketing/docs search should reuse the command-palette interaction pattern with a docs index backend when `durablestack.com` is built.
- Planned next UX track: wire live external providers and email sign-in link, then add onboarding flow.

## Notes

- `NuGet.Config` is committed to use `nuget.org` package source in this repo.
- API and App `appsettings.json` include starter local connection strings.
