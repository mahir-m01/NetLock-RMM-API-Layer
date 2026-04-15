// ─────────────────────────────────────────────────────────────────────────────
// HealthEndpointTests.cs
// Integration tests for the /health and /devices endpoints.
//
// WHAT IS AN INTEGRATION TEST?
// Unlike unit tests (which test logic in isolation with mocks), integration tests
// spin up the REAL application and make HTTP calls against it. This catches issues
// that unit tests cannot: middleware ordering, DI misconfiguration, routing bugs,
// serialization errors, etc.
//
// HOW DOES WebApplicationFactory WORK?
// Microsoft.AspNetCore.Mvc.Testing provides WebApplicationFactory<T> — a class that
// starts your ASP.NET Core application in-memory (no real HTTP server, no real port).
// It creates an HttpClient pre-configured to talk to this in-memory server.
//
// The generic type parameter T must be a public type from the API assembly. Since
// Program.cs uses top-level statements (no explicit `public class Program`), the
// generated Program class is internal and cannot be referenced from another project.
// We use HealthEndpoints (a public static class in the API) as the anchor type.
// WebApplicationFactory finds the entry point assembly from HealthEndpoints' assembly.
//
// ENVIRONMENT SETUP:
// The app reads configuration from appsettings.json and appsettings.{Environment}.json.
// We set ASPNETCORE_ENVIRONMENT=Development so appsettings.Development.json is loaded,
// which contains the real connection strings for the local dev MySQL and NetLock hub.
//
// CONNECTION DETAILS (local dev stack):
//   MySQL:   Server=localhost;Port=3306;Database=iphbmh;User=root;Password=EuMmvIqcJjafr6fb;
//   NetLock: http://localhost:7080/commandHub
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Tests.Integration;

// System.Net provides HttpStatusCode enum (HttpStatusCode.OK = 200, etc.)
using System.Net;

// We reference TenantContext as the anchor type for WebApplicationFactory.
// TenantContext is a public, non-static class in the API assembly — it satisfies
// the type constraint. WebApplicationFactory uses this type only to locate the
// assembly — it does NOT call any methods on TenantContext.
//
// Why not Program? Program.cs uses top-level statements, which generates an
// `internal` class — unreachable from a separate test project without modifying src/.
// Any public non-static class from the API assembly works as the anchor type.
using ControlIT.Api.Application;

// Microsoft.AspNetCore.Mvc.Testing provides WebApplicationFactory<T> and related types.
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

/// <summary>
/// Integration tests that spin up the full ControlIT API in-memory and
/// make real HTTP requests to verify endpoint behavior.
///
/// IClassFixture<ControlItWebApplicationFactory>: xUnit's mechanism for sharing
/// a single factory instance across all tests in this class. This is important for
/// performance — starting the app (connecting to MySQL, connecting to SignalR) takes
/// time. IClassFixture ensures the app starts once and is reused for all tests.
///
/// In Jest, the equivalent would be beforeAll() + afterAll() for the entire describe block.
/// </summary>
public class HealthEndpointTests : IClassFixture<ControlItWebApplicationFactory>
{
    // The factory that creates the in-memory test server.
    // All tests in this class share the same factory instance (IClassFixture).
    private readonly ControlItWebApplicationFactory _factory;

    /// <summary>
    /// Constructor — xUnit injects the factory via IClassFixture.
    /// This is called ONCE before all tests in this class run.
    /// In Jest terms: this runs at the top of the describe block, not inside beforeEach.
    /// </summary>
    public HealthEndpointTests(ControlItWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// [Fact] — tests that GET /health returns HTTP 200 OK without any API key.
    ///
    /// WHY /health is exempt from auth: monitoring tools (uptime checkers, Kubernetes probes,
    /// load balancers) must be able to poll the health endpoint without credentials.
    /// ApiKeyMiddleware explicitly skips /health paths:
    ///   if (context.Request.Path.StartsWithSegments("/health")) { await _next(context); return; }
    ///
    /// Expected behavior:
    ///   No x-api-key header → still returns 200 (or 503 if services are down, but NOT 401)
    ///
    /// PREREQUISITE: MySQL must be running at localhost:3306 and NetLock at localhost:7080.
    /// Run with: dotnet test --filter "Category=Integration" (requires local dev stack).
    /// Skip in CI without a dev stack: dotnet test --filter "Category!=Integration"
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetHealth_ReturnsOk_WithoutApiKey()
    {
        // Arrange — create an HttpClient that talks to the in-memory test server.
        // CreateClient() sets the BaseAddress to the in-memory server automatically.
        // No headers needed — /health is exempt from auth.
        var client = _factory.CreateClient();

        // Act — make a real GET /health request to the in-memory server.
        // HttpResponseMessage contains the status code, headers, and body.
        var response = await client.GetAsync("/health");

        // Assert — /health should return 200 OK (healthy) or 503 (degraded/unhealthy).
        // It must NOT return 401 Unauthorized, which would mean the auth bypass failed.
        //
        // We check that the status is NOT 401, because the actual status depends on
        // whether MySQL and SignalR are available in the test environment:
        //   - If both are up: 200 OK
        //   - If SignalR/MySQL is down: 503 Service Unavailable
        // In both cases, the endpoint reached the handler (not rejected by auth).
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);

        // Additionally: verify the response is either 200 or 503 (not 404 or 500).
        // 404 would mean the /health route wasn't registered. 500 would mean a crash.
        var validHealthCodes = new[] { HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable };
        Assert.Contains(response.StatusCode, validHealthCodes);
    }

    /// <summary>
    /// [Fact] — tests that GET /devices returns HTTP 401 Unauthorized without an API key.
    ///
    /// WHY /devices requires auth: it returns tenant-scoped device data.
    /// Without an API key, ApiKeyMiddleware returns 401 immediately:
    ///   context.Response.StatusCode = 401;
    ///   await context.Response.WriteAsJsonAsync(new { error = "API key required." });
    ///   return;
    ///
    /// This test verifies the auth middleware is correctly enforced on all non-exempt routes.
    ///
    /// PREREQUISITE: MySQL must be running at localhost:3306 and NetLock at localhost:7080.
    /// Run with: dotnet test --filter "Category=Integration" (requires local dev stack).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetDevices_Returns401_WithoutApiKey()
    {
        // Arrange — no x-api-key header → should be rejected with 401
        var client = _factory.CreateClient();

        // Act — make a real GET /devices?page=1&pageSize=5 request.
        // We include query params because DeviceEndpoints may validate them,
        // but the middleware check happens BEFORE the endpoint handler runs.
        var response = await client.GetAsync("/devices?page=1&pageSize=5");

        // Assert — must be 401 Unauthorized (no other status is acceptable here)
        // HttpStatusCode.Unauthorized is the enum value for HTTP 401.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

/// <summary>
/// Custom WebApplicationFactory that configures the test server to use the
/// Development environment, loading appsettings.Development.json with the
/// real local connection strings.
///
/// WHY a custom factory? WebApplicationFactory<T> by default uses the Production
/// environment configuration. We need Development config to pick up:
///   - MySQL connection string (localhost:3306)
///   - NetLock hub URL (localhost:7080)
///   - NetLock admin session token
///
/// HOW IT WORKS:
/// ConfigureWebHost is called during factory initialization. We override the
/// environment and add any additional configuration overrides here.
///
/// INHERITANCE: WebApplicationFactory<TenantContext> — we use TenantContext
/// as the anchor type because it's a public, non-static class in the API assembly.
/// WebApplicationFactory resolves the API's entry point from this type's assembly.
/// TenantContext is only used to locate the assembly — it is never instantiated
/// or called by the factory itself.
/// </summary>
public class ControlItWebApplicationFactory : WebApplicationFactory<TenantContext>
{
    /// <summary>
    /// Override ConfigureWebHost to customize the test server configuration.
    /// This is called once when the factory creates the first test server instance.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseEnvironment("Development") causes ASP.NET Core to load:
        //   appsettings.json          (base config)
        //   appsettings.Development.json  (dev overrides — has real MySQL/NetLock values)
        //
        // Without this, the factory defaults to "Production" environment, where
        // appsettings.Development.json is NOT loaded, and the connection strings
        // would be missing → app startup would throw InvalidOperationException.
        builder.UseEnvironment("Development");

        // ConfigureAppConfiguration allows us to inject additional configuration
        // values that override anything in appsettings.*.json files.
        // This is useful for test-specific overrides without modifying config files.
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add any test-specific config overrides here if needed in the future.
            // For now, appsettings.Development.json provides all required values.
            //
            // Example of how you'd override a value:
            //   config.AddInMemoryCollection(new Dictionary<string, string?>
            //   {
            //       ["ConnectionStrings:ControlIt"] = "Server=testdb;...",
            //   });
        });
    }
}
