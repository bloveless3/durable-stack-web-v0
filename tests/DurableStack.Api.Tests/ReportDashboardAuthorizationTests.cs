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
using Microsoft.Extensions.Configuration;
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
        Assert.Empty(body.FailureGroups);
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

    [Fact]
    public async Task ReportDashboardQuery_WithHybridRollupEnabled_MergesRollupAndRawData()
    {
        using var factory = new DurableStackApiFactory(enableHybridRollupReads: true);
        var user = await factory.SeedUserScopeAsync();
        await factory.SeedHybridDashboardTelemetryAsync(user.AllowedTenantPublicId);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.CreateUserToken(user.UserId, "reports.read"));

        var response = await client.PostAsJsonAsync("/v1/reports/dashboard/query", new ReportDashboardQueryRequest
        {
            TenantIds = [user.AllowedTenantId],
            Timeframe = "last_24h"
        });

        var body = await response.Content.ReadFromJsonAsync<ReportDashboardResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("last_24h", body.Timeframe);
        Assert.Equal("15m", body.BucketSize);
        Assert.Equal(5, body.Summary.RunStarted);
        Assert.Equal(4, body.Summary.RunSucceeded);
        Assert.Equal(2, body.Summary.RunFailed);
        Assert.Equal(1, body.Summary.RunRetried);
        Assert.Equal(49, body.Summary.HeartbeatCount);

        Assert.Equal(body.Summary.RunStarted, body.Series.Sum(x => x.RunStarted));
        Assert.Equal(body.Summary.RunSucceeded, body.Series.Sum(x => x.RunSucceeded));
        Assert.Equal(body.Summary.RunFailed, body.Series.Sum(x => x.RunFailed));
        Assert.Equal(body.Summary.RunRetried, body.Series.Sum(x => x.RunRetried));
        Assert.Equal(body.Summary.HeartbeatCount, body.Series.Sum(x => x.HeartbeatCount));
        Assert.Equal(30.0, body.Summary.P95DurationMs);

        Assert.Contains(body.Workers.Items, x => x.WorkerName == "worker-hybrid");
        Assert.Contains(body.Workers.Items, x => x.Status == "offline");

        var mergedFailure = Assert.Single(body.FailureGroups, x =>
            x.JobName == "nightly-job" &&
            x.ErrorType == "TimeoutException" &&
            x.ErrorMessage == "Upstream API timed out.");

        Assert.Equal(6, mergedFailure.FailureCount);
    }

    [Fact]
    public async Task ReportDashboardQuery_WithHybridEnabledAndNoRollups_FallsBackToFullRawWindow()
    {
        using var factory = new DurableStackApiFactory(enableHybridRollupReads: true);
        var user = await factory.SeedUserScopeAsync();
        await factory.SeedHybridFallbackRawOnlyTelemetryAsync(user.AllowedTenantPublicId);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.CreateUserToken(user.UserId, "reports.read"));

        var response = await client.PostAsJsonAsync("/v1/reports/dashboard/query", new ReportDashboardQueryRequest
        {
            TenantIds = [user.AllowedTenantId],
            Timeframe = "last_24h"
        });

        var body = await response.Content.ReadFromJsonAsync<ReportDashboardResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.Summary.RunStarted);
        Assert.Equal(2, body.Summary.RunSucceeded);
        Assert.Equal(0, body.Summary.RunFailed);
        Assert.Equal(30, body.Summary.HeartbeatCount);
        Assert.Equal(2, body.Series.Sum(x => x.RunStarted));
        Assert.Equal(2, body.Series.Sum(x => x.RunSucceeded));
        Assert.Equal(30, body.Series.Sum(x => x.HeartbeatCount));
    }

    [Fact]
    public async Task ReportDashboardQuery_WithHybridEnabled_DoesNotDoubleCountBoundaryBucket()
    {
        using var factory = new DurableStackApiFactory(enableHybridRollupReads: true);
        var user = await factory.SeedUserScopeAsync();
        await factory.SeedHybridBoundaryTelemetryAsync(user.AllowedTenantPublicId);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.CreateUserToken(user.UserId, "reports.read"));

        var response = await client.PostAsJsonAsync("/v1/reports/dashboard/query", new ReportDashboardQueryRequest
        {
            TenantIds = [user.AllowedTenantId],
            Timeframe = "last_24h"
        });

        var body = await response.Content.ReadFromJsonAsync<ReportDashboardResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(5, body.Summary.RunStarted);
        Assert.Equal(5, body.Summary.RunSucceeded);
        Assert.Equal(50, body.Summary.HeartbeatCount);
        Assert.Equal(5, body.Series.Sum(x => x.RunStarted));
        Assert.Equal(5, body.Series.Sum(x => x.RunSucceeded));
        Assert.Equal(50, body.Series.Sum(x => x.HeartbeatCount));
    }

    private sealed class DurableStackApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _controlPlaneDbName = $"control-{Guid.NewGuid():N}";
        private readonly string _telemetryDbName = $"telemetry-{Guid.NewGuid():N}";
        private readonly bool _enableHybridRollupReads;
        private const string Issuer = "DurableStack.App";
        private const string Audience = "DurableStack.Api";
        private const string SigningKey = "dev-only-signing-key-change-me-please-32chars";

        public DurableStackApiFactory(bool enableHybridRollupReads = false)
        {
            _enableHybridRollupReads = enableHybridRollupReads;
        }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["TelemetryLifecycle:ExecutionMode"] = "local",
                    ["TelemetryLifecycle:RollupWorker:Enabled"] = "false",
                    ["TelemetryLifecycle:RetentionWorker:Enabled"] = "false",
                    ["TelemetryLifecycle:Query:EnableHybridRollupReads"] = _enableHybridRollupReads ? "true" : "false"
                };

                configurationBuilder.AddInMemoryCollection(settings);
            });

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

        public async Task SeedHybridDashboardTelemetryAsync(string tenantPublicId)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

            var nowUtc = DateTimeOffset.UtcNow;
            var rollupBucketStartUtc = AlignToBucket(nowUtc.AddHours(-2), TimeSpan.FromMinutes(15));

            db.TelemetryBucketRollups.Add(new TelemetryBucketRollup
            {
                Id = Guid.NewGuid(),
                TenantPublicId = tenantPublicId,
                BucketSize = "15m",
                BucketStartUtc = rollupBucketStartUtc,
                RunStarted = 4,
                RunSucceeded = 3,
                RunFailed = 1,
                RunRetried = 1,
                HeartbeatCount = 40,
                LastEventAtUtc = rollupBucketStartUtc.AddMinutes(14),
                ComputedAtUtc = nowUtc
            });

            db.TelemetryFailureGroupRollups.Add(new TelemetryFailureGroupRollup
            {
                Id = Guid.NewGuid(),
                TenantPublicId = tenantPublicId,
                BucketSize = "15m",
                BucketStartUtc = rollupBucketStartUtc,
                JobName = "nightly-job",
                ErrorType = "TimeoutException",
                ErrorMessage = "Upstream API timed out.",
                FailureCount = 5,
                FirstOccurredAtUtc = rollupBucketStartUtc,
                LastOccurredAtUtc = rollupBucketStartUtc.AddMinutes(14),
                ComputedAtUtc = nowUtc
            });

            var batch = new TelemetryBatch
            {
                Id = Guid.NewGuid(),
                TenantPublicId = tenantPublicId,
                ReceivedAtUtc = nowUtc,
                AcceptedCount = 4,
                RejectedCount = 0
            };

            batch.Events.Add(new TelemetryEvent
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                EventType = "job_started",
                EventVersion = 2,
                OccurredAtUtc = nowUtc.AddMinutes(-10),
                WorkerName = "worker-hybrid",
                JobName = "nightly-job"
            });

            batch.Events.Add(new TelemetryEvent
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                EventType = "job_succeeded",
                EventVersion = 2,
                OccurredAtUtc = nowUtc.AddMinutes(-10),
                WorkerName = "worker-hybrid",
                JobName = "nightly-job",
                DurationMs = 12.0
            });

            batch.Events.Add(new TelemetryEvent
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                EventType = "job_failed",
                EventVersion = 2,
                OccurredAtUtc = nowUtc.AddMinutes(-8),
                WorkerName = "worker-hybrid",
                JobName = "nightly-job",
                ErrorType = "TimeoutException",
                ErrorMessage = "Upstream API timed out.",
                DurationMs = 30.0
            });

            batch.Events.Add(new TelemetryEvent
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                EventType = "worker_heartbeat_batch",
                EventVersion = 2,
                OccurredAtUtc = nowUtc.AddMinutes(-5),
                WorkerName = "worker-hybrid",
                HeartbeatCount = 9,
                PayloadJson = "{\"heartbeatCount\":9}"
            });

            db.TelemetryBatches.Add(batch);
            await db.SaveChangesAsync();
        }

        public async Task SeedHybridFallbackRawOnlyTelemetryAsync(string tenantPublicId)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

            var nowUtc = DateTimeOffset.UtcNow;

            db.TelemetryBatches.Add(CreateBatch(
                tenantPublicId,
                nowUtc,
                "worker-raw-only",
                "raw-job",
                nowUtc.AddHours(-3),
                heartbeatCount: 21));

            db.TelemetryBatches.Add(CreateBatch(
                tenantPublicId,
                nowUtc,
                "worker-raw-only",
                "raw-job",
                nowUtc.AddMinutes(-15),
                heartbeatCount: 9));

            await db.SaveChangesAsync();
        }

        public async Task SeedHybridBoundaryTelemetryAsync(string tenantPublicId)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

            var nowUtc = DateTimeOffset.UtcNow;
            var boundaryUtc = AlignToBucket(nowUtc.AddHours(-1), TimeSpan.FromMinutes(15));

            db.TelemetryBucketRollups.Add(new TelemetryBucketRollup
            {
                Id = Guid.NewGuid(),
                TenantPublicId = tenantPublicId,
                BucketSize = "15m",
                BucketStartUtc = boundaryUtc.AddMinutes(-15),
                RunStarted = 4,
                RunSucceeded = 4,
                RunFailed = 0,
                RunRetried = 0,
                HeartbeatCount = 40,
                LastEventAtUtc = boundaryUtc.AddMinutes(-1),
                ComputedAtUtc = nowUtc
            });

            db.TelemetryBatches.Add(CreateBatch(
                tenantPublicId,
                nowUtc,
                "worker-boundary",
                "boundary-job",
                boundaryUtc,
                heartbeatCount: 10));

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

        private static DateTimeOffset AlignToBucket(DateTimeOffset value, TimeSpan bucketInterval)
        {
            var ticks = value.UtcDateTime.Ticks;
            var bucketTicks = bucketInterval.Ticks;
            var alignedTicks = ticks - (ticks % bucketTicks);
            return new DateTimeOffset(alignedTicks, TimeSpan.Zero);
        }

        private static TelemetryBatch CreateBatch(
            string tenantPublicId,
            DateTimeOffset receivedAtUtc,
            string workerName,
            string jobName,
            DateTimeOffset occurredAtUtc,
            int heartbeatCount)
        {
            var batch = new TelemetryBatch
            {
                Id = Guid.NewGuid(),
                TenantPublicId = tenantPublicId,
                ReceivedAtUtc = receivedAtUtc,
                AcceptedCount = 3,
                RejectedCount = 0
            };

            batch.Events.Add(new TelemetryEvent
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                EventType = "job_started",
                EventVersion = 2,
                OccurredAtUtc = occurredAtUtc,
                WorkerName = workerName,
                JobName = jobName
            });

            batch.Events.Add(new TelemetryEvent
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                EventType = "job_succeeded",
                EventVersion = 2,
                OccurredAtUtc = occurredAtUtc,
                WorkerName = workerName,
                JobName = jobName,
                DurationMs = 12.0
            });

            batch.Events.Add(new TelemetryEvent
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                EventType = "worker_heartbeat_batch",
                EventVersion = 2,
                OccurredAtUtc = occurredAtUtc,
                WorkerName = workerName,
                HeartbeatCount = heartbeatCount,
                PayloadJson = $"{{\"heartbeatCount\":{heartbeatCount}}}"
            });

            return batch;
        }
    }

    private sealed record SeededUserScope(Guid UserId, Guid AllowedTenantId, string AllowedTenantPublicId);
}
