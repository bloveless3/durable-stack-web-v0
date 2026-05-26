using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Claims;
using System.Text;
using DurableStack.Api.Services;
using DurableStack.ControlPlane;
using DurableStack.ControlPlane.Entities;
using DurableStack.Platform.Contracts;
using DurableStack.Telemetry;
using DurableStack.Telemetry.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DurableStack.Api.Tests;

public sealed class ReportDashboardAuthorizationTests
{
    [Fact]
    public async Task ReportDashboardQuery_WithoutBearerToken_ReturnsUnauthorized()
    {
        using var factory = new DurableStackApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.PostAsJsonAsync("/v1/reports/dashboard/query", new ReportDashboardQueryRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReportDashboardQuery_WithoutReportsReadScope_ReturnsForbidden()
    {
        using var factory = new DurableStackApiFactory();
        var user = await factory.SeedUserScopeAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.CreateUserToken(user.UserId, "profile.read"));

        var response = await client.PostAsJsonAsync("/v1/reports/dashboard/query", new ReportDashboardQueryRequest());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReportDashboardQuery_WithAuthorizedScope_ReturnsDashboardPayload()
    {
        using var factory = new DurableStackApiFactory();
        var user = await factory.SeedUserScopeAsync();
        await factory.SeedDashboardTelemetryAsync(user.AllowedTenantPublicId);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.CreateUserToken(user.UserId, "reports.read"));

        var response = await client.PostAsJsonAsync("/v1/reports/dashboard/query", new ReportDashboardQueryRequest
        {
            TenantIds = [user.AllowedTenantId],
            Timeframe = "last_hour"
        });

        var body = await response.Content.ReadFromJsonAsync<ReportDashboardResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("last_hour", body.Timeframe);
        Assert.Equal("1m", body.BucketSize);
        Assert.True(body.Series.Count >= 1);
        Assert.Equal(1, body.Summary.RunStarted);
        Assert.Equal(1, body.Summary.RunSucceeded);
        Assert.Equal(0, body.Summary.RunFailed);
        Assert.Equal(10, body.Summary.HeartbeatCount);
        Assert.Equal("Report Project - Production", body.Workers.Items[0].TenantDisplayName);
        Assert.Contains(body.Workers.Items, x => x.WorkerName == "worker-a");
    }

    [Fact]
    public async Task ReportDashboardQuery_RequestingUnauthorizedTenant_ReturnsEmptyDashboard()
    {
        using var factory = new DurableStackApiFactory();
        var user = await factory.SeedUserScopeAsync();

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.CreateUserToken(user.UserId, "reports.read"));

        var query = new ReportDashboardQueryRequest
        {
            TenantIds = [Guid.NewGuid()]
        };

        var response = await client.PostAsJsonAsync("/v1/reports/dashboard/query", query);
        var body = await response.Content.ReadFromJsonAsync<ReportDashboardResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(0, body.Summary.RunsTotal);
        Assert.Empty(body.ScopeTenantIds);
    }

    [Fact]
    public async Task ReportDashboardQuery_WithInvalidTimeframe_ReturnsBadRequest()
    {
        using var factory = new DurableStackApiFactory();
        var user = await factory.SeedUserScopeAsync();

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.CreateUserToken(user.UserId, "reports.read"));

        var response = await client.PostAsJsonAsync("/v1/reports/dashboard/query", new ReportDashboardQueryRequest
        {
            Timeframe = "all_time"
        });

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_timeframe", json.RootElement.GetProperty("code").GetString());
    }

    private sealed class DurableStackApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _controlPlaneDbName = $"control-{Guid.NewGuid():N}";
        private readonly string _telemetryDbName = $"telemetry-{Guid.NewGuid():N}";
        private const string Issuer = "DurableStack.App";
        private const string Audience = "DurableStack.Api";
        private const string SigningKey = "dev-only-signing-key-change-me-please-32chars";

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ControlPlaneDbContext>>();
                services.RemoveAll<DbContextOptions<TelemetryDbContext>>();
                services.RemoveAll<ControlPlaneDbContext>();
                services.RemoveAll<TelemetryDbContext>();

                services.AddDbContext<ControlPlaneDbContext>(options =>
                    options.UseInMemoryDatabase(_controlPlaneDbName));

                services.AddDbContext<TelemetryDbContext>(options =>
                    options.UseInMemoryDatabase(_telemetryDbName));
            });
        }

        public async Task<SeededUserScope> SeedUserScopeAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();

            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = $"user-{Guid.NewGuid():N}@example.test",
                DisplayName = "Report User",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var org = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Report Org",
                CreatedByUserId = user.Id,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var member = new OrganizationMember
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
                Name = "Report Project",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                EnvironmentName = "Production",
                PublicTenantId = $"tenant_{Guid.NewGuid():N}",
                SyncEnabled = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            db.Users.Add(user);
            db.Organizations.Add(org);
            db.OrganizationMembers.Add(member);
            db.Projects.Add(project);
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            return new SeededUserScope(userId, tenant.Id, tenant.PublicTenantId);
        }

        public async Task SeedDashboardTelemetryAsync(string tenantPublicId)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

            var batch = new TelemetryBatch
            {
                Id = Guid.NewGuid(),
                TenantPublicId = tenantPublicId,
                ReceivedAtUtc = DateTimeOffset.UtcNow,
                AcceptedCount = 3,
                RejectedCount = 0
            };

            batch.Events.Add(new TelemetryEvent
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                EventType = "job_started",
                EventVersion = 2,
                OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
                WorkerName = "worker-a",
                JobName = "heartbeat-every-minute"
            });

            batch.Events.Add(new TelemetryEvent
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                EventType = "job_succeeded",
                EventVersion = 2,
                OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
                WorkerName = "worker-a",
                JobName = "heartbeat-every-minute",
                DurationMs = 8.4
            });

            batch.Events.Add(new TelemetryEvent
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                EventType = "worker_heartbeat_batch",
                EventVersion = 2,
                OccurredAtUtc = DateTimeOffset.UtcNow.AddSeconds(-4),
                WorkerName = "worker-a",
                HeartbeatCount = 10,
                PayloadJson = "{\"heartbeatCount\":10}"
            });

            db.TelemetryBatches.Add(batch);
            await db.SaveChangesAsync();
        }

        public string CreateUserToken(Guid userId, string scope)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
                new(ClaimTypes.NameIdentifier, userId.ToString("D")),
                new("scope", scope)
            };

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                notBefore: DateTime.UtcNow.AddMinutes(-1),
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                    SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    private sealed record SeededUserScope(Guid UserId, Guid AllowedTenantId, string AllowedTenantPublicId);
}
