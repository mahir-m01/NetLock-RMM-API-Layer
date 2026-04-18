namespace ControlIT.Api.Tests.Integration;

using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ControlIT.Api.Application;
using ControlIT.Api.Domain.Models;
using ControlIT.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class HealthEndpointTests : IClassFixture<ControlItWebApplicationFactory>
{
    private readonly ControlItWebApplicationFactory _factory;

    public HealthEndpointTests(ControlItWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetHealth_ReturnsOk_WithoutAuth()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        var validCodes = new[] { HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable };
        Assert.Contains(response.StatusCode, validCodes);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetHealthz_ReturnsOk_WithoutAuth()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetDevices_Returns401_WithoutToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/devices?page=1&pageSize=5");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetDevices_Returns401_WithApiKeyHeaderOnly()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", "any-key-value");
        var response = await client.GetAsync("/devices?page=1&pageSize=5");
        // API key header is ignored — must return 401 (JWT required)
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

/// <summary>
/// Shared factory for integration tests. Sets JWT signing key env var so the
/// in-memory API can start. Exposes a helper to issue test JWT tokens.
/// </summary>
public class ControlItWebApplicationFactory : WebApplicationFactory<TenantContext>
{
    internal const string TestSigningKey = "integration-test-signing-key-32bytes!!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set env vars before the app reads them in Program.cs.
        Environment.SetEnvironmentVariable("CONTROLIT_JWT_SIGNING_KEY", TestSigningKey);
        Environment.SetEnvironmentVariable("CONTROLIT_BOOTSTRAP_PASSWORD", "TestBootstrap@2026!");
        Environment.SetEnvironmentVariable("CONTROLIT_BOOTSTRAP_EMAIL", "admin@test.local");

        builder.UseEnvironment("Development");
    }

    /// <summary>Issues a valid JWT for the given role/tenantId, ready for Bearer auth.</summary>
    internal string IssueToken(Role role = Role.SuperAdmin, int? tenantId = null)
    {
        Environment.SetEnvironmentVariable("CONTROLIT_JWT_SIGNING_KEY", TestSigningKey);
        var jwtService = new JwtService(NullLogger<JwtService>.Instance);

        var user = new ControlItUser
        {
            Id = 999,
            Email = "test@test.local",
            PasswordHash = "x",
            Role = role,
            TenantId = tenantId
        };

        return jwtService.IssueAccessToken(user);
    }
}
