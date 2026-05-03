namespace ControlIT.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ControlIT.Api.Application;
using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.Models;
using ControlIT.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

[Collection("Database")]
public class RateLimitPartitionTests
{
    private readonly ControlItWebApplicationFactory _factory;

    public RateLimitPartitionTests(MySqlContainerFixture dbFixture)
    {
        _factory = new ControlItWebApplicationFactory
        {
            DatabaseConnectionString = dbFixture.ConnectionString
        };
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ApiLimiter_WhenUserAExhaustsBucket_DoesNotBlockUserB()
    {
        await WithRateLimitOverrideAsync("RateLimiting__Api__PermitLimit", "2", async () =>
        {
            using var factory = _factory.WithWebHostBuilder(_ => { });
            var userA = CreateClientWithToken(factory, userId: 1001);
            var userB = CreateClientWithToken(factory, userId: 1002);

            Assert.Equal(HttpStatusCode.OK, (await userA.GetAsync("/auth/me")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await userA.GetAsync("/auth/me")).StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, (await userA.GetAsync("/auth/me")).StatusCode);

            Assert.Equal(HttpStatusCode.OK, (await userB.GetAsync("/auth/me")).StatusCode);
        });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ApiLimiter_UnauthenticatedProtectedRequest_Returns401_Not429()
    {
        await WithRateLimitOverrideAsync("RateLimiting__Api__PermitLimit", "1", async () =>
        {
            using var factory = _factory.WithWebHostBuilder(_ => { });
            var client = factory.CreateClient();

            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
        });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CommandsLimiter_WhenUserAExhaustsBucket_DoesNotBlockUserB()
    {
        await WithRateLimitOverrideAsync("RateLimiting__Commands__PermitLimit", "2", async () =>
        {
            using var factory = _factory.WithWebHostBuilder(_ => { });
            var userA = CreateClientWithToken(factory, userId: 2001, role: Role.Technician, tenantId: 1);
            var userB = CreateClientWithToken(factory, userId: 2002, role: Role.Technician, tenantId: 1);
            var invalidCommand = new CommandRequest
            {
                DeviceId = 0,
                Command = "whoami",
                Shell = "cmd",
                TimeoutSeconds = 30
            };

            Assert.Equal(HttpStatusCode.BadRequest, (await userA.PostAsJsonAsync("/commands/execute", invalidCommand)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await userA.PostAsJsonAsync("/commands/execute", invalidCommand)).StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, (await userA.PostAsJsonAsync("/commands/execute", invalidCommand)).StatusCode);

            Assert.Equal(HttpStatusCode.BadRequest, (await userB.PostAsJsonAsync("/commands/execute", invalidCommand)).StatusCode);
        });
    }

    private HttpClient CreateClientWithToken(
        WebApplicationFactory<TenantContext> factory,
        int userId,
        Role role = Role.SuperAdmin,
        int? tenantId = null)
    {
        var client = factory.CreateClient();
        var token = _factory.IssueToken(role, tenantId, userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task WithRateLimitOverrideAsync(
        string key,
        string value,
        Func<Task> action)
    {
        var previous = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);

        try
        {
            await action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
        }
    }
}
