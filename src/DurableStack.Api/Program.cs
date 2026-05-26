using System.Text.Json;
using System.Text;
using DurableStack.Api.Services;
using DurableStack.ControlPlane;
using DurableStack.ControlPlane.DependencyInjection;
using DurableStack.ControlPlane.Entities;
using DurableStack.Platform.Contracts;
using DurableStack.Telemetry;
using DurableStack.Telemetry.DependencyInjection;
using DurableStack.Telemetry.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Primitives;
using Npgsql;

const string CorrelationIdHeaderName = "X-Correlation-Id";
const string TenantHeaderName = "X-DurableStack-TenantId";
const string SecretHeaderName = "X-DurableStack-ClientSecret";
const int MaxRequestBytes = 1024 * 1024;
const int MaxPayloadJsonLength = 65535;
const int MaxCorrelationIdLength = 128;

var builder = WebApplication.CreateBuilder(args);

var docsBaseUrl = NormalizeDocsBaseUrl(builder.Configuration["Documentation:BaseUrl"]);
var userJwtOptions = builder.Configuration.GetSection(UserJwtOptions.SectionPath).Get<UserJwtOptions>()
    ?? new UserJwtOptions();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<IUserReportAccessService, UserReportAccessService>();
builder.Services.AddScoped<IReportDashboardQueryService, ReportDashboardQueryService>();

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(userJwtOptions.SigningKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = userJwtOptions.RequireHttpsMetadata;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = userJwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = userJwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ReportsRead", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {
            var scopeClaim = context.User.FindFirst("scope")?.Value;
            if (string.IsNullOrWhiteSpace(scopeClaim))
            {
                return false;
            }

            var scopes = scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return scopes.Contains("reports.read", StringComparer.Ordinal);
        });
    });
});

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddControlPlanePostgres(builder.Configuration);
    builder.Services.AddTelemetryPostgres(builder.Configuration);
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var controlPlaneDb = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
    var telemetryDb = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
    controlPlaneDb.Database.Migrate();
    telemetryDb.Database.Migrate();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    var correlationId = ResolveCorrelationId(context);
    context.Response.Headers[CorrelationIdHeaderName] = correlationId;
    await next();
});

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
    HttpContext context,
    TelemetryBatchRequest payload,
    ControlPlaneDbContext controlPlaneDb,
    TelemetryDbContext telemetryDb,
    CancellationToken cancellationToken) =>
{
    var correlationId = ResolveCorrelationId(context);

    if (context.Request.ContentLength is > MaxRequestBytes)
    {
        return CreateProblem(
            statusCode: StatusCodes.Status413PayloadTooLarge,
            title: "Request payload is too large.",
            detail: $"The request body must be <= {MaxRequestBytes} bytes.",
            type: BuildProblemTypeUrl(docsBaseUrl, "payload-too-large"),
            correlationId: correlationId);
    }

    var authResult = await context.Request.TryAuthenticateAsync(controlPlaneDb, cancellationToken);
    if (!authResult.Success || authResult.TenantId is null)
    {
        return CreateAuthProblem(authResult, correlationId, docsBaseUrl);
    }

    var tenant = await controlPlaneDb.Tenants
        .AsNoTracking()
        .SingleOrDefaultAsync(x => x.PublicTenantId == authResult.TenantId, cancellationToken);

    if (tenant is null)
    {
        return CreateProblem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            detail: "Authentication failed for tenant credentials.",
            type: BuildProblemTypeUrl(docsBaseUrl, "auth-failed"),
            correlationId: correlationId,
            code: "invalid_credentials");
    }

    if (!tenant.SyncEnabled)
    {
        return CreateProblem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Telemetry sync is disabled.",
            detail: "Ingestion is disabled for this tenant.",
            type: BuildProblemTypeUrl(docsBaseUrl, "sync-disabled"),
            correlationId: correlationId,
            code: "sync_disabled");
    }

    var validationErrors = ValidatePayload(payload, tenant.PublicTenantId, tenant.MaxBatchSize);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(
            validationErrors,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Telemetry batch validation failed.",
            type: BuildProblemTypeUrl(docsBaseUrl, "validation-failed"),
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "validation_failed",
                ["correlationId"] = correlationId
            });
    }

    var idempotencyKey = NormalizeOptional(payload.IdempotencyKey);

    if (idempotencyKey is not null)
    {
        var priorBatch = await telemetryDb.TelemetryBatches
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.TenantPublicId == tenant.PublicTenantId && x.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (priorBatch is not null)
        {
            return Results.Ok(ToResponse(priorBatch, isDuplicate: true, correlationId));
        }
    }

    var batch = new TelemetryBatch
    {
        Id = Guid.NewGuid(),
        TenantPublicId = tenant.PublicTenantId,
        ServiceName = NormalizeOptional(payload.ServiceName),
        EnvironmentName = NormalizeOptional(payload.EnvironmentName),
        IdempotencyKey = idempotencyKey,
        ReceivedAtUtc = DateTimeOffset.UtcNow,
        AcceptedCount = payload.Events.Count,
        RejectedCount = 0
    };

    foreach (var item in payload.Events)
    {
        var errorMessage = tenant.DetailedErrorSyncEnabled ? NormalizeOptional(item.ErrorMessage) : null;

        batch.Events.Add(new TelemetryEvent
        {
            Id = Guid.NewGuid(),
            BatchId = batch.Id,
            EventType = item.EventType.Trim(),
            EventVersion = item.EventVersion,
            OccurredAtUtc = item.OccurredAtUtc,
            JobName = NormalizeOptional(item.JobName),
            RunId = item.RunId,
            Attempt = item.Attempt,
            WorkerName = NormalizeOptional(item.WorkerName),
            DurationMs = item.DurationMs,
            ErrorType = NormalizeOptional(item.ErrorType),
            ErrorMessage = errorMessage,
            PayloadJson = NormalizeOptional(item.PayloadJson),
            HeartbeatCount = TryExtractHeartbeatCount(item.EventType, item.PayloadJson)
        });
    }

    telemetryDb.TelemetryBatches.Add(batch);

    try
    {
        await telemetryDb.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException ex) when (idempotencyKey is not null && IsIdempotencyConflict(ex))
    {
        var priorBatch = await telemetryDb.TelemetryBatches
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.TenantPublicId == tenant.PublicTenantId && x.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (priorBatch is not null)
        {
            return Results.Ok(ToResponse(priorBatch, isDuplicate: true, correlationId));
        }

        throw;
    }

    return Results.Ok(ToResponse(batch, isDuplicate: false, correlationId));
});

app.MapPost("/v1/reports/dashboard/query", async (
    HttpContext context,
    ReportDashboardQueryRequest? payload,
    ControlPlaneDbContext controlPlaneDb,
    IUserReportAccessService accessService,
    IReportDashboardQueryService dashboardQueryService,
    CancellationToken cancellationToken) =>
{
    var correlationId = ResolveCorrelationId(context);

    if (!UserAccessContext.TryCreate(context.User, out var user) || user is null)
    {
        return CreateProblem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            detail: "A valid user access token is required.",
            type: BuildProblemTypeUrl(docsBaseUrl, "auth-failed"),
            correlationId: correlationId,
            code: "invalid_user_token");
    }

    UserDashboardReportScope scope;

    try
    {
        scope = await accessService.BuildDashboardScopeAsync(user, payload, cancellationToken);
    }
    catch (UserReportScopeException ex)
    {
        return CreateProblem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid report query.",
            detail: ex.Message,
            type: BuildProblemTypeUrl(docsBaseUrl, "validation-failed"),
            correlationId: correlationId,
            code: ex.Code);
    }

    var queryRunAtUtc = DateTimeOffset.UtcNow;

    if (scope.TenantIds.Count == 0)
    {
        return Results.Ok(new ReportDashboardResponse
        {
            ScopeTenantIds = [],
            Timeframe = scope.Timeframe,
            WindowStartUtc = scope.WindowStartUtc,
            WindowEndUtc = scope.WindowEndUtc,
            BucketSize = scope.BucketSize,
            QueryRunAtUtc = queryRunAtUtc,
            Summary = new ReportDashboardSummary()
        });
    }

    var tenantMappings = await controlPlaneDb.Tenants
        .AsNoTracking()
        .Where(x => scope.TenantIds.Contains(x.Id))
        .Select(x => new
        {
            x.Id,
            x.PublicTenantId,
            x.EnvironmentName,
            ProjectName = x.Project != null ? x.Project.Name : null
        })
        .ToListAsync(cancellationToken);

    var scopeTenantIds = tenantMappings
        .Select(x => x.Id.ToString("D"))
        .ToArray();

    var tenantPublicIds = tenantMappings
        .Select(x => x.PublicTenantId)
        .ToArray();

    var tenantDisplayByPublicId = tenantMappings
        .ToDictionary(
            x => x.PublicTenantId,
            x => BuildTenantDisplayName(x.ProjectName, x.EnvironmentName),
            StringComparer.Ordinal);

    var response = await dashboardQueryService.QueryAsync(
        scope,
        tenantPublicIds,
        scopeTenantIds,
        tenantDisplayByPublicId,
        queryRunAtUtc,
        cancellationToken);

    return Results.Ok(response);
}).RequireAuthorization("ReportsRead");

app.Run();

return;

static IResult CreateAuthProblem(RequestAuthResult authResult, string correlationId, string docsBaseUrl)
{
    var code = authResult.FailureReason switch
    {
        RequestAuthFailureReason.MissingHeaders => "missing_headers",
        RequestAuthFailureReason.InvalidHeaders => "invalid_headers",
        _ => "invalid_credentials"
    };

    var detail = code switch
    {
        "missing_headers" => "Required authentication headers are missing.",
        "invalid_headers" => "Authentication headers are present but invalid.",
        _ => "Authentication failed for tenant credentials."
    };

    var extensions = new Dictionary<string, object?>
    {
        ["code"] = code,
        ["correlationId"] = correlationId
    };

    if (code == "missing_headers")
    {
        extensions["requiredHeaders"] = new[] { TenantHeaderName, SecretHeaderName };
    }

    return Results.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Unauthorized",
        detail: detail,
        type: BuildProblemTypeUrl(docsBaseUrl, "auth-failed"),
        extensions: extensions);
}

static IResult CreateProblem(
    int statusCode,
    string title,
    string detail,
    string type,
    string correlationId,
    string? code = null)
{
    var extensions = new Dictionary<string, object?>
    {
        ["correlationId"] = correlationId
    };

    if (!string.IsNullOrWhiteSpace(code))
    {
        extensions["code"] = code;
    }

    return Results.Problem(
        statusCode: statusCode,
        title: title,
        detail: detail,
        type: type,
        extensions: extensions);
}

static TelemetryBatchResponse ToResponse(TelemetryBatch batch, bool isDuplicate, string correlationId)
{
    return new TelemetryBatchResponse
    {
        AcceptedCount = batch.AcceptedCount,
        RejectedCount = batch.RejectedCount,
        ServerTimeUtc = DateTimeOffset.UtcNow,
        IdempotencyKey = batch.IdempotencyKey,
        IsDuplicate = isDuplicate,
        CorrelationId = correlationId
    };
}

static Dictionary<string, string[]> ValidatePayload(
    TelemetryBatchRequest payload,
    string authenticatedTenantId,
    int maxBatchSize)
{
    var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

    if (!string.IsNullOrWhiteSpace(payload.TenantId) &&
        !string.Equals(payload.TenantId.Trim(), authenticatedTenantId, StringComparison.OrdinalIgnoreCase))
    {
        AddError("tenantId", "TenantId in payload must match authenticated tenant header.");
    }

    ValidateOptionalLength(payload.IdempotencyKey, 200, "idempotencyKey");
    ValidateOptionalLength(payload.ServiceName, 200, "serviceName");
    ValidateOptionalLength(payload.EnvironmentName, 100, "environmentName");

    if (payload.Events.Count == 0)
    {
        AddError("events", "At least one event is required.");
    }

    if (payload.Events.Count > maxBatchSize)
    {
        AddError("events", $"Batch cannot exceed {maxBatchSize} events for this tenant.");
    }

    var now = DateTimeOffset.UtcNow;
    var oldestAllowed = now.AddDays(-30);
    var newestAllowed = now.AddMinutes(10);

    for (var i = 0; i < payload.Events.Count; i++)
    {
        var item = payload.Events[i];
        var prefix = $"events[{i}]";

        if (string.IsNullOrWhiteSpace(item.EventType))
        {
            AddError($"{prefix}.eventType", "EventType is required.");
        }
        else if (item.EventType.Trim().Length > 100)
        {
            AddError($"{prefix}.eventType", "EventType cannot exceed 100 characters.");
        }

        if (item.EventVersion <= 0)
        {
            AddError($"{prefix}.eventVersion", "EventVersion must be greater than zero.");
        }

        if (item.OccurredAtUtc < oldestAllowed || item.OccurredAtUtc > newestAllowed)
        {
            AddError(
                $"{prefix}.occurredAtUtc",
                "OccurredAtUtc must be within 30 days in the past and 10 minutes in the future.");
        }

        if (item.Attempt is < 0)
        {
            AddError($"{prefix}.attempt", "Attempt cannot be negative.");
        }

        if (item.DurationMs is < 0)
        {
            AddError($"{prefix}.durationMs", "DurationMs cannot be negative.");
        }

        ValidateOptionalLength(item.JobName, 200, $"{prefix}.jobName");
        ValidateOptionalLength(item.WorkerName, 200, $"{prefix}.workerName");
        ValidateOptionalLength(item.ErrorType, 200, $"{prefix}.errorType");
        ValidateOptionalLength(item.ErrorMessage, 4000, $"{prefix}.errorMessage");

        if (!string.IsNullOrWhiteSpace(item.PayloadJson))
        {
            if (item.PayloadJson.Length > MaxPayloadJsonLength)
            {
                AddError(
                    $"{prefix}.payloadJson",
                    $"PayloadJson cannot exceed {MaxPayloadJsonLength} characters.");
            }
            else
            {
                try
                {
                    using var _ = JsonDocument.Parse(item.PayloadJson);
                }
                catch (JsonException)
                {
                    AddError($"{prefix}.payloadJson", "PayloadJson must be valid JSON.");
                }
            }
        }
    }

    return errors.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.Ordinal);

    void AddError(string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages))
        {
            messages = new List<string>();
            errors[key] = messages;
        }

        messages.Add(message);
    }

    void ValidateOptionalLength(string? value, int maxLength, string key)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length > maxLength)
        {
            AddError(key, $"{key} cannot exceed {maxLength} characters.");
        }
    }
}

static string ResolveCorrelationId(HttpContext context)
{
    if (context.Items.TryGetValue(CorrelationIdHeaderName, out var existing) && existing is string value)
    {
        return value;
    }

    var correlationId = TryGetFirstHeaderValue(context.Request.Headers, CorrelationIdHeaderName);
    if (string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = context.TraceIdentifier;
    }
    else
    {
        correlationId = correlationId.Trim();
        if (correlationId.Length > MaxCorrelationIdLength)
        {
            correlationId = correlationId[..MaxCorrelationIdLength];
        }
    }

    context.Items[CorrelationIdHeaderName] = correlationId;
    return correlationId;
}

static string? NormalizeOptional(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

static int? TryExtractHeartbeatCount(string eventType, string? payloadJson)
{
    if (!string.Equals(eventType?.Trim(), "worker_heartbeat_batch", StringComparison.Ordinal))
    {
        return null;
    }

    if (string.IsNullOrWhiteSpace(payloadJson))
    {
        return 0;
    }

    try
    {
        using var document = JsonDocument.Parse(payloadJson);
        if (!document.RootElement.TryGetProperty("heartbeatCount", out var heartbeatElement))
        {
            return 0;
        }

        if (!heartbeatElement.TryGetInt32(out var heartbeatCount))
        {
            return 0;
        }

        return heartbeatCount < 0 ? 0 : heartbeatCount;
    }
    catch (JsonException)
    {
        return 0;
    }
}

static string? TryGetFirstHeaderValue(IHeaderDictionary headers, string key)
{
    if (!headers.TryGetValue(key, out StringValues values))
    {
        return null;
    }

    foreach (var value in values)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
    }

    return null;
}

static string BuildTenantDisplayName(string? projectName, string? environmentName)
{
    var normalizedProject = string.IsNullOrWhiteSpace(projectName)
        ? "Project"
        : projectName.Trim();

    var normalizedEnvironment = string.IsNullOrWhiteSpace(environmentName)
        ? "Environment"
        : environmentName.Trim();

    return $"{normalizedProject} - {normalizedEnvironment}";
}

static bool IsIdempotencyConflict(DbUpdateException exception)
{
    return exception.InnerException is PostgresException postgresException &&
           postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
}

static string NormalizeDocsBaseUrl(string? configuredBaseUrl)
{
    var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
        ? "https://docs.durablestack.com"
        : configuredBaseUrl.Trim();

    return baseUrl.TrimEnd('/');
}

static string BuildProblemTypeUrl(string docsBaseUrl, string slug)
{
    return $"{docsBaseUrl}/problems/{slug}";
}

public partial class Program;
