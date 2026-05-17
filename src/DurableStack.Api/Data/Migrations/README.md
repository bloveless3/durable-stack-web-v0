# DurableStack.Api EF Migrations

Use separate migrations for each DbContext.

Example commands from repository root:

```bash
dotnet ef migrations add InitialControlPlane \
  --project src/DurableStack.ControlPlane \
  --startup-project src/DurableStack.Api \
  --context DurableStack.ControlPlane.ControlPlaneDbContext \
  --output-dir Migrations/ControlPlane

dotnet ef migrations add InitialTelemetry \
  --project src/DurableStack.Telemetry \
  --startup-project src/DurableStack.Api \
  --context DurableStack.Telemetry.TelemetryDbContext \
  --output-dir Migrations/Telemetry
```
