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

public sealed class ReportSummaryAuthorizationTests
{
    [Fact]
    public async Task ReportSummaryQuery_WithoutBearerToken_ReturnsUnauthorized()
    {
        using var factory = new DurableStackApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.PostAsJsonAsync("/v1/reports/summary/query", new ReportSummaryQueryRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReportSummaryQuery_WithoutReportsReadScope_ReturnsForbidden()
    {
        using var factory = new DurableStackApiFactory();
        var user = await factory.SeedUserScopeAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.CreateUserToken(user.UserId, "profile.read"));

        var response = await client.PostAsJsonAsync("/v1/reports/summary/query", new ReportSummaryQueryRequest());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReportSummaryQuery_WithAuthorizedScope_ReturnsTenantScopedSummary()
    {
        using var factory = new DurableStackApiFactory();
        var user = await factory.SeedUserScopeAsync();
        await factory.SeedTelemetryAsync(user.AllowedTenantPublicId);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.CreateUserToken(user.UserId, "reports.read"));

        var query = new ReportSummaryQueryRequest
        {
            TenantIds = [user.AllowedTenantId],
            FromUtc = DateTimeOffset.UtcNow.AddHours(-1),
            ToUtc = DateTimeOffset.UtcNow.AddHours(1)
        };

        var response = await client.PostAsJsonAsync("/v1/reports/summary/query", query);
        var body = await response.Content.ReadFromJsonAsync<ReportSummaryResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.TotalEvents);
        Assert.Equal(1, body.FailedEvents);
        Assert.Contains(user.AllowedTenantId.ToString("D"), body.ScopeTenantIds);
        Assert.NotNull(body.NextCursor);
        Assert.True(body.QueryRunAtUtc > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task ReportSummaryQuery_RequestingUnauthorizedTenant_ReturnsEmptySummary()
    {
        using var factory = new DurableStackApiFactory();
        var user = await factory.SeedUserScopeAsync();

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.CreateUserToken(user.UserId, "reports.read"));

        var query = new ReportSummaryQueryRequest
        {
            TenantIds = [Guid.NewGuid()]
        };

        var response = await client.PostAsJsonAsync("/v1/reports/summary/query", query);
        var body = await response.Content.ReadFromJsonAsync<ReportSummaryResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(0, body.TotalEvents);
        Assert.Empty(body.ScopeTenantIds);
        Assert.NotNull(body.NextCursor);
    }

    [Fact]
    public async Task ReportSummaryQuery_WithInvalidSinceCursor_ReturnsBadRequest()
    {
        using var factory = new DurableStackApiFactory();
        var user = await factory.SeedUserScopeAsync();

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.CreateUserToken(user.UserId, "reports.read"));

        var query = new ReportSummaryQueryRequest
        {
            SinceCursor = "not-a-valid-cursor"
        };

        var response = await client.PostAsJsonAsync("/v1/reports/summary/query", query);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_since_cursor", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ReportSummaryQuery_SupportsIncrementalCursorPolling()
    {
        using var factory = new DurableStackApiFactory();
        var user = await factory.SeedUserScopeAsync();
        await factory.SeedTelemetryAsync(user.AllowedTenantPublicId);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.CreateUserToken(user.UserId, "reports.read"));

        var firstResponse = await client.PostAsJsonAsync("/v1/reports/summary/query", new ReportSummaryQueryRequest
        {
            TenantIds = [user.AllowedTenantId]
        });

        var firstBody = await firstResponse.Content.ReadFromJsonAsync<ReportSummaryResponse>();
        Assert.NotNull(firstBody);
        Assert.NotNull(firstBody.NextCursor);

        await Task.Delay(20);
        await factory.SeedTelemetryEventAsync(user.AllowedTenantPublicId, "job_completed");

        var incrementalResponse = await client.PostAsJsonAsync("/v1/reports/summary/query", new ReportSummaryQueryRequest
        {
            TenantIds = [user.AllowedTenantId],
            SinceCursor = firstBody.NextCursor
        });

        var incrementalBody = await incrementalResponse.Content.ReadFromJsonAsync<ReportSummaryResponse>();
        Assert.NotNull(incrementalBody);
        Assert.Equal(HttpStatusCode.OK, incrementalResponse.StatusCode);
        Assert.True(incrementalBody.TotalEvents >= 1);
        Assert.NotEqual(firstBody.NextCursor, incrementalBody.NextCursor);
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

        public async Task SeedTelemetryAsync(string tenantPublicId)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

            var batch = new TelemetryBatch
            {
                Id = Guid.NewGuid(),
                TenantPublicId = tenantPublicId,
                ReceivedAtUtc = DateTimeOffset.UtcNow,
                AcceptedCount = 2,
                RejectedCount = 0
            };

            batch.Events.Add(new TelemetryEvent
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                EventType = "job_completed",
                EventVersion = 1,
                OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
            });

            batch.Events.Add(new TelemetryEvent
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                EventType = "job_failed",
                EventVersion = 1,
                OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
            });

            db.TelemetryBatches.Add(batch);
            await db.SaveChangesAsync();
        }

        public async Task SeedTelemetryEventAsync(string tenantPublicId, string eventType)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

            var batch = new TelemetryBatch
            {
                Id = Guid.NewGuid(),
                TenantPublicId = tenantPublicId,
                ReceivedAtUtc = DateTimeOffset.UtcNow,
                AcceptedCount = 1,
                RejectedCount = 0
            };

            batch.Events.Add(new TelemetryEvent
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                EventType = eventType,
                EventVersion = 1,
                OccurredAtUtc = DateTimeOffset.UtcNow
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
