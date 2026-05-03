namespace ControlIT.Api.Tests.Integration;

using System.Net;
using ControlIT.Api.Application;
using ControlIT.Api.Domain.Models;
using ControlIT.Api.Infrastructure.Auth;
using ControlIT.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

[Collection("Database")]
public class HealthEndpointTests
{
    private readonly ControlItWebApplicationFactory _factory;

    public HealthEndpointTests(MySqlContainerFixture dbFixture)
    {
        _factory = new ControlItWebApplicationFactory
        {
            DatabaseConnectionString = dbFixture.ConnectionString
        };
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
/// Shared factory for integration tests. Sets required environment variables so
/// the in-memory API can start against the Testcontainers MySQL instance.
///
/// Root cause of the original timing bug (and why env vars are the correct fix):
///
/// Program.cs uses WebApplication.CreateBuilder, which WebApplicationFactory
/// intercepts via DeferredHostBuilder. ConfigureWebHost callbacks run before
/// Program.Main is invoked — so Environment.SetEnvironmentVariable calls in the
/// ConfigureWebHost body are visible when Program.cs line 55 reads
/// CONTROLIT_DB_CONNECTION and line 206 reads CONTROLIT_JWT_SIGNING_KEY.
///
/// IWebHostBuilder.ConfigureServices and ConfigureAppConfiguration callbacks
/// are NOT forwarded to WebApplicationBuilder in the DeferredHostBuilder path
/// and should not be used here.
///
/// Additionally, user secrets loaded in Development override appsettings.json's
/// Database:Name = "netlock" with the real production value. Setting Database__Name
/// via env var wins over user secrets (env vars are loaded last in the pipeline)
/// and ensures the schema validator queries the correct database name on the container.
/// </summary>
public class ControlItWebApplicationFactory : WebApplicationFactory<TenantContext>
{
    internal const string TestSigningKey = "integration-test-signing-key-32bytes!!";

    /// <summary>
    /// When set, the factory injects this as CONTROLIT_DB_CONNECTION so Program.cs
    /// reads the test container's connection string at startup.
    /// </summary>
    internal string? DatabaseConnectionString { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // All env vars must be set in the ConfigureWebHost body — not in any
        // ConfigureServices/ConfigureAppConfiguration callback — because they must
        // be present before Program.Main executes.
        Environment.SetEnvironmentVariable("CONTROLIT_JWT_SIGNING_KEY", TestSigningKey);
        Environment.SetEnvironmentVariable("CONTROLIT_BOOTSTRAP_PASSWORD", "TestBootstrap@2026!");
        Environment.SetEnvironmentVariable("CONTROLIT_BOOTSTRAP_EMAIL", "admin@test.local");
        Environment.SetEnvironmentVariable("CONTROLIT_DISABLE_NETLOCK_LIVE_BRIDGE", "true");

        if (!string.IsNullOrWhiteSpace(DatabaseConnectionString))
        {
            // Override the connection string so the API and schema validator both
            // target the ephemeral test container instead of the dev MySQL instance.
            Environment.SetEnvironmentVariable("CONTROLIT_DB_CONNECTION", DatabaseConnectionString);

            // Override Database:Name so the schema validator's information_schema query
            // uses "netlock" — the database name the test container was created with.
            // User secrets (loaded in Development) can override the appsettings.json
            // default of "netlock" with the real production database name; this env var
            // wins over user secrets because environment variables are loaded last.
            Environment.SetEnvironmentVariable("Database__Name", "netlock");
        }

        builder.UseEnvironment("Development");
    }

    /// <summary>Issues a valid JWT for the given role/tenantId, ready for Bearer auth.</summary>
    internal string IssueToken(Role role = Role.SuperAdmin, int? tenantId = null, int userId = 999)
    {
        Environment.SetEnvironmentVariable("CONTROLIT_JWT_SIGNING_KEY", TestSigningKey);
        var jwtService = new JwtService(NullLogger<JwtService>.Instance);

        var user = new ControlItUser
        {
            Id = userId,
            Email = "test@test.local",
            PasswordHash = "x",
            Role = role,
            TenantId = tenantId
        };

        return jwtService.IssueAccessToken(user);
    }
}
