using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DurableStack.Api.Services;
using DurableStack.ControlPlane;
using DurableStack.ControlPlane.Entities;
using DurableStack.Platform.Contracts;
using DurableStack.Telemetry;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DurableStack.Api.Tests;

public sealed class TelemetryIngestionTests
{
    [Fact]
    public async Task PostBatch_WithValidPayload_ReturnsAcceptedResponse()
    {
        using var factory = new DurableStackApiFactory();
        var tenant = await factory.SeedTenantAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add("X-DurableStack-TenantId", tenant.PublicTenantId);
        client.DefaultRequestHeaders.Add("X-DurableStack-ClientSecret", tenant.ClientSecret);
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "tool-run-001");

        var payload = CreateValidPayload();
        var response = await client.PostAsJsonAsync("/v1/events/batch", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-Id"));

        var body = await response.Content.ReadFromJsonAsync<TelemetryBatchResponse>();
        Assert.NotNull(body);
        Assert.Equal(1, body.AcceptedCount);
        Assert.Equal(0, body.RejectedCount);
        Assert.False(body.IsDuplicate);
        Assert.Equal("tool-run-001", body.CorrelationId);
    }

    [Fact]
    public async Task PostBatch_WithoutAuthHeaders_ReturnsDetailedUnauthorizedProblem()
    {
        using var factory = new DurableStackApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.PostAsJsonAsync("/v1/events/batch", CreateValidPayload());
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Unauthorized", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("missing_headers", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostBatch_WithInvalidEvent_ReturnsValidationProblem()
    {
        using var factory = new DurableStackApiFactory();
        var tenant = await factory.SeedTenantAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add("X-DurableStack-TenantId", tenant.PublicTenantId);
        client.DefaultRequestHeaders.Add("X-DurableStack-ClientSecret", tenant.ClientSecret);

        var payload = CreateValidPayload();
        payload.Events[0].EventType = " ";

        var response = await client.PostAsJsonAsync("/v1/events/batch", payload);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Telemetry batch validation failed.", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("validation_failed", body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("events[0].eventType", out _));
    }

    [Fact]
    public async Task PostBatch_WithInvalidPayloadJson_ReturnsValidationProblem()
    {
        using var factory = new DurableStackApiFactory();
        var tenant = await factory.SeedTenantAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add("X-DurableStack-TenantId", tenant.PublicTenantId);
        client.DefaultRequestHeaders.Add("X-DurableStack-ClientSecret", tenant.ClientSecret);

        var payload = CreateValidPayload();
        payload.Events[0].PayloadJson = "{not-json}";

        var response = await client.PostAsJsonAsync("/v1/events/batch", payload);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("events[0].payloadJson", out _));
    }

    [Fact]
    public async Task PostBatch_WithIdempotencyKey_DeduplicatesSecondSubmission()
    {
        using var factory = new DurableStackApiFactory();
        var tenant = await factory.SeedTenantAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add("X-DurableStack-TenantId", tenant.PublicTenantId);
        client.DefaultRequestHeaders.Add("X-DurableStack-ClientSecret", tenant.ClientSecret);

        var payload = CreateValidPayload();
        payload.IdempotencyKey = "tool-batch-0001";

        var first = await client.PostAsJsonAsync("/v1/events/batch", payload);
        var second = await client.PostAsJsonAsync("/v1/events/batch", payload);

        var firstBody = await first.Content.ReadFromJsonAsync<TelemetryBatchResponse>();
        var secondBody = await second.Content.ReadFromJsonAsync<TelemetryBatchResponse>();

        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.False(firstBody.IsDuplicate);
        Assert.True(secondBody.IsDuplicate);

        using var scope = factory.Services.CreateScope();
        var telemetryDb = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
        Assert.Equal(1, await telemetryDb.TelemetryBatches.CountAsync());
        Assert.Equal(1, await telemetryDb.TelemetryEvents.CountAsync());
    }

    private static TelemetryBatchRequest CreateValidPayload()
    {
        return new TelemetryBatchRequest
        {
            ServiceName = "durable-worker",
            EnvironmentName = "Production",
            Events =
            [
                new TelemetryEventDto
                {
                    EventType = "job_completed",
                    EventVersion = 1,
                    OccurredAtUtc = DateTimeOffset.UtcNow,
                    RunId = Guid.NewGuid(),
                    JobName = "NightlySync",
                    Attempt = 1,
                    WorkerName = "Worker-A",
                    DurationMs = 123.45,
                    PayloadJson = "{\"result\":\"ok\"}"
                }
            ]
        };
    }

    private sealed class DurableStackApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _controlPlaneDbName = $"control-{Guid.NewGuid():N}";
        private readonly string _telemetryDbName = $"telemetry-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ControlPlaneDbContext>>();
                services.RemoveAll<DbContextOptions<TelemetryDbContext>>();
                services.RemoveAll<ControlPlaneDbContext>();
                services.RemoveAll<TelemetryDbContext>();

                var npgsqlDescriptors = services
                    .Where(descriptor =>
                        descriptor.ImplementationType?.Namespace?.Contains("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal) == true)
                    .ToList();

                foreach (var descriptor in npgsqlDescriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ControlPlaneDbContext>(options =>
                    options.UseInMemoryDatabase(_controlPlaneDbName));

                services.AddDbContext<TelemetryDbContext>(options =>
                    options.UseInMemoryDatabase(_telemetryDbName));
            });
        }

        public async Task<SeededTenant> SeedTenantAsync()
        {
            var tenantPublicId = $"tenant_{Guid.NewGuid():N}";
            var secret = "test-secret-123";

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"user-{Guid.NewGuid():N}@example.test",
                DisplayName = "Test User",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var org = new Organization
            {
                Id = Guid.NewGuid(),
                Name = $"Org-{Guid.NewGuid():N}",
                CreatedByUserId = user.Id,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var membership = new OrganizationMember
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                UserId = user.Id,
                Role = "Owner",
                JoinedAtUtc = DateTimeOffset.UtcNow
            };

            var project = new Project
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                Name = $"Project-{Guid.NewGuid():N}",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                EnvironmentName = "Production",
                PublicTenantId = tenantPublicId,
                SyncEnabled = true,
                DetailedErrorSyncEnabled = true,
                MaxBatchSize = 1000,
                RecommendedBatchIntervalSeconds = 15,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var credential = new TenantCredential
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                CredentialName = "default",
                ClientSecretHash = CredentialHasher.Hash(secret),
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            db.Users.Add(user);
            db.Organizations.Add(org);
            db.OrganizationMembers.Add(membership);
            db.Projects.Add(project);
            db.Tenants.Add(tenant);
            db.TenantCredentials.Add(credential);
            await db.SaveChangesAsync();

            return new SeededTenant(tenantPublicId, secret);
        }
    }

    private sealed record SeededTenant(string PublicTenantId, string ClientSecret);
}
