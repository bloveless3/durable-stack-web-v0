using DurableStack.Api.Services;
using DurableStack.ControlPlane;
using DurableStack.ControlPlane.Entities;
using DurableStack.ControlPlane.DependencyInjection;
using DurableStack.Platform.Contracts;
using DurableStack.Telemetry;
using DurableStack.Telemetry.DependencyInjection;
using DurableStack.Telemetry.Entities;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddControlPlanePostgres(builder.Configuration);
builder.Services.AddTelemetryPostgres(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapGet("/version", () => Results.Ok(new
{
    service = "DurableStack.Api",
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0"
}));

app.MapPost("/dev/bootstrap/tenant", async (
    ControlPlaneDbContext controlPlaneDb,
    CancellationToken cancellationToken) =>
{
    var user = new User
    {
        Id = Guid.NewGuid(),
        Email = "founder@durablestack.local",
        DisplayName = "DurableStack Founder",
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    var organization = new Organization
    {
        Id = Guid.NewGuid(),
        Name = "Demo Org",
        CreatedByUserId = user.Id,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    var membership = new OrganizationMember
    {
        Id = Guid.NewGuid(),
        OrganizationId = organization.Id,
        UserId = user.Id,
        Role = "Owner",
        JoinedAtUtc = DateTimeOffset.UtcNow
    };

    var project = new Project
    {
        Id = Guid.NewGuid(),
        OrganizationId = organization.Id,
        Name = "Demo Project",
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    var tenant = new Tenant
    {
        Id = Guid.NewGuid(),
        ProjectId = project.Id,
        EnvironmentName = "Development",
        PublicTenantId = $"tenant_{Guid.NewGuid():N}",
        SyncEnabled = true,
        DetailedErrorSyncEnabled = false,
        MaxBatchSize = 1000,
        RecommendedBatchIntervalSeconds = 15,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    var rawSecret = ClientSecretGenerator.Generate();
    var credential = new TenantCredential
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.Id,
        CredentialName = "default",
        ClientSecretHash = CredentialHasher.Hash(rawSecret),
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    controlPlaneDb.Users.Add(user);
    controlPlaneDb.Organizations.Add(organization);
    controlPlaneDb.OrganizationMembers.Add(membership);
    controlPlaneDb.Projects.Add(project);
    controlPlaneDb.Tenants.Add(tenant);
    controlPlaneDb.TenantCredentials.Add(credential);
    await controlPlaneDb.SaveChangesAsync(cancellationToken);

    return Results.Ok(new
    {
        tenantId = tenant.PublicTenantId,
        clientSecret = rawSecret,
        environment = tenant.EnvironmentName,
        maxBatchSize = tenant.MaxBatchSize
    });
}).WithDescription("Local-only helper to create a demo tenant and credential.");

app.MapPost("/v1/events/batch", async (
    HttpRequest request,
    TelemetryBatchRequest payload,
    ControlPlaneDbContext controlPlaneDb,
    TelemetryDbContext telemetryDb,
    CancellationToken cancellationToken) =>
{
    var (success, tenantId) = await request.TryAuthenticateAsync(controlPlaneDb, cancellationToken);
    if (!success || tenantId is null)
    {
        return Results.Unauthorized();
    }

    if (payload.Events.Count == 0)
    {
        return Results.BadRequest(new { error = "At least one event is required." });
    }

    var tenant = await controlPlaneDb.Tenants
        .AsNoTracking()
        .SingleOrDefaultAsync(x => x.PublicTenantId == tenantId, cancellationToken);

    if (tenant is null)
    {
        return Results.Unauthorized();
    }

    if (!tenant.SyncEnabled)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (payload.Events.Count > tenant.MaxBatchSize)
    {
        return Results.BadRequest(new
        {
            error = "Batch exceeds maximum allowed events.",
            maxBatchSize = tenant.MaxBatchSize
        });
    }

    var idempotencyKey = string.IsNullOrWhiteSpace(payload.IdempotencyKey)
        ? null
        : payload.IdempotencyKey.Trim();

    if (idempotencyKey is not null)
    {
        var priorBatch = await telemetryDb.TelemetryBatches
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.TenantPublicId == tenantId && x.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (priorBatch is not null)
        {
            return Results.Ok(new TelemetryBatchResponse
            {
                AcceptedCount = priorBatch.AcceptedCount,
                RejectedCount = priorBatch.RejectedCount,
                ServerTimeUtc = DateTimeOffset.UtcNow,
                IdempotencyKey = idempotencyKey
            });
        }
    }

    var batch = new TelemetryBatch
    {
        Id = Guid.NewGuid(),
        TenantPublicId = tenantId,
        ServiceName = payload.ServiceName,
        EnvironmentName = payload.EnvironmentName,
        IdempotencyKey = idempotencyKey,
        ReceivedAtUtc = DateTimeOffset.UtcNow,
        AcceptedCount = payload.Events.Count,
        RejectedCount = 0
    };

    foreach (var item in payload.Events)
    {
        var errorMessage = tenant.DetailedErrorSyncEnabled ? item.ErrorMessage : null;

        batch.Events.Add(new TelemetryEvent
        {
            Id = Guid.NewGuid(),
            BatchId = batch.Id,
            EventType = item.EventType,
            EventVersion = item.EventVersion,
            OccurredAtUtc = item.OccurredAtUtc,
            JobName = item.JobName,
            RunId = item.RunId,
            Attempt = item.Attempt,
            WorkerName = item.WorkerName,
            DurationMs = item.DurationMs,
            ErrorType = item.ErrorType,
            ErrorMessage = errorMessage,
            PayloadJson = item.PayloadJson
        });
    }

    telemetryDb.TelemetryBatches.Add(batch);
    await telemetryDb.SaveChangesAsync(cancellationToken);

    return Results.Ok(new TelemetryBatchResponse
    {
        AcceptedCount = batch.AcceptedCount,
        RejectedCount = batch.RejectedCount,
        ServerTimeUtc = DateTimeOffset.UtcNow,
        IdempotencyKey = idempotencyKey
    });
});

app.MapGet("/v1/reports/summary", async (
    HttpRequest request,
    ControlPlaneDbContext controlPlaneDb,
    TelemetryDbContext telemetryDb,
    CancellationToken cancellationToken) =>
{
    var (success, tenantId) = await request.TryAuthenticateAsync(controlPlaneDb, cancellationToken);
    if (!success || tenantId is null)
    {
        return Results.Unauthorized();
    }

    var events = telemetryDb.TelemetryEvents
        .AsNoTracking()
        .Where(x => x.Batch != null && x.Batch.TenantPublicId == tenantId);

    var totalEvents = await events.CountAsync(cancellationToken);
    var failedEvents = await events.CountAsync(x => x.EventType == "job_failed", cancellationToken);
    var lastEventAtUtc = await events
        .OrderByDescending(x => x.OccurredAtUtc)
        .Select(x => (DateTimeOffset?)x.OccurredAtUtc)
        .FirstOrDefaultAsync(cancellationToken);

    return Results.Ok(new ReportSummaryResponse
    {
        TenantId = tenantId,
        TotalEvents = totalEvents,
        FailedEvents = failedEvents,
        LastEventAtUtc = lastEventAtUtc
    });
});

app.Run();
