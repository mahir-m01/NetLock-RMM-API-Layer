// ─────────────────────────────────────────────────────────────────────────────
// EndpointIntegrationTests.cs
// Integration tests for all business endpoints: devices, dashboard, events,
// tenants, audit logs, commands, and auth guard spot-checks.
//
// Uses ControlItWebApplicationFactory (defined in HealthEndpointTests.cs) to
// spin up the full API in-memory against the real local dev stack:
//   MySQL:   localhost:3306, database iphbmh
//   NetLock: localhost:7080/commandHub
//   Auth:    JWT Bearer token issued via ControlItWebApplicationFactory.IssueToken()
//
// Run with: dotnet test --filter "Category=Integration"
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControlIT.Api.Domain.Models;
using Xunit;

/// <summary>
/// Integration tests for all business endpoints. One [Fact] per behaviour.
/// All tests share a single HttpClient with a SuperAdmin JWT pre-set via IClassFixture.
/// </summary>
public class EndpointIntegrationTests : IClassFixture<ControlItWebApplicationFactory>
{
    private readonly ControlItWebApplicationFactory _factory;
    private readonly HttpClient _client;

    // ── Private DTOs for deserialisation ──────────────────────────────────────
    // Defined here so tests are independent of the API's internal DTO namespace.
    // Properties use JsonPropertyName to handle camelCase JSON from System.Text.Json.

    private record PagedResponse<T>(
        [property: JsonPropertyName("items")] IEnumerable<T> Items,
        [property: JsonPropertyName("totalCount")] int TotalCount,
        [property: JsonPropertyName("page")] int Page,
        [property: JsonPropertyName("pageSize")] int PageSize);

    private record DeviceDto(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("deviceName")] string DeviceName,
        [property: JsonPropertyName("platform")] string Platform);

    private record DashboardDto(
        [property: JsonPropertyName("totalDevices")] int TotalDevices,
        [property: JsonPropertyName("onlineDevices")] int OnlineDevices,
        [property: JsonPropertyName("totalTenants")] int TotalTenants,
        [property: JsonPropertyName("totalEvents")] int TotalEvents);

    // ── Constructor ───────────────────────────────────────────────────────────

    public EndpointIntegrationTests(ControlItWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        var token = factory.IssueToken(Role.SuperAdmin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    // ── GET /devices ──────────────────────────────────────────────────────────

    /// <summary>
    /// GET /devices?page=1&amp;pageSize=10 should return 200 with a paged result body.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetDevices_ReturnsOk_WithPagedBody()
    {
        var response = await _client.GetAsync("/devices?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// GET /devices?page=1&amp;pageSize=10 body must contain items, totalCount, page, pageSize.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetDevices_Body_HasRequiredPagedFields()
    {
        var response = await _client.GetAsync("/devices?page=1&pageSize=10");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<PagedResponse<DeviceDto>>();

        Assert.NotNull(body);
        Assert.NotNull(body.Items);
        Assert.True(body.TotalCount >= 0, "totalCount must be >= 0");
        Assert.Equal(1, body.Page);
        Assert.Equal(10, body.PageSize);
    }

    // ── GET /devices/{id} — existing device ──────────────────────────────────

    /// <summary>
    /// GET /devices/27 (real device in DB) should return 200 with a device object.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetDeviceById_ExistingId_ReturnsOk()
    {
        var response = await _client.GetAsync("/devices/27");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// GET /devices/27 body must contain id, deviceName, platform and id == 27.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetDeviceById_ExistingId_HasRequiredFields()
    {
        var response = await _client.GetAsync("/devices/27");
        response.EnsureSuccessStatusCode();

        var device = await response.Content.ReadFromJsonAsync<DeviceDto>();

        Assert.NotNull(device);
        Assert.Equal(27, device.Id);
        Assert.False(string.IsNullOrEmpty(device.DeviceName), "deviceName must not be empty");
        Assert.False(string.IsNullOrEmpty(device.Platform), "platform must not be empty");
    }

    // ── GET /devices/{id} — non-existent device ───────────────────────────────

    /// <summary>
    /// GET /devices/99999 (non-existent) should return 404 Not Found.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetDeviceById_NonExistentId_Returns404()
    {
        var response = await _client.GetAsync("/devices/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GET /dashboard ────────────────────────────────────────────────────────

    /// <summary>
    /// GET /dashboard should return 200.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetDashboard_ReturnsOk()
    {
        var response = await _client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// GET /dashboard body must contain totalDevices, onlineDevices, totalTenants, totalEvents
    /// all >= 0.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetDashboard_Body_HasRequiredFields()
    {
        var response = await _client.GetAsync("/dashboard");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<DashboardDto>();

        Assert.NotNull(body);
        Assert.True(body.TotalDevices >= 0, "totalDevices must be >= 0");
        Assert.True(body.OnlineDevices >= 0, "onlineDevices must be >= 0");
        Assert.True(body.TotalTenants >= 0, "totalTenants must be >= 0");
        Assert.True(body.TotalEvents >= 0, "totalEvents must be >= 0");
    }

    // ── GET /events ───────────────────────────────────────────────────────────

    /// <summary>
    /// GET /events?page=1&amp;pageSize=10 should return 200 with a paged result body.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetEvents_ReturnsOk_WithPagedBody()
    {
        var response = await _client.GetAsync("/events?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// GET /events?page=1&amp;pageSize=10 body totalCount must be >= 0.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetEvents_Body_TotalCountIsNonNegative()
    {
        var response = await _client.GetAsync("/events?page=1&pageSize=10");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<PagedResponse<JsonElement>>();

        Assert.NotNull(body);
        Assert.True(body.TotalCount >= 0, "totalCount must be >= 0");
    }

    // ── GET /tenants ──────────────────────────────────────────────────────────

    /// <summary>
    /// GET /tenants should return 200 with a non-null body.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetTenants_ReturnsOk_WithNonNullBody()
    {
        var response = await _client.GetAsync("/tenants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<IEnumerable<JsonElement>>();
        Assert.NotNull(body);
    }

    // ── GET /audit/logs ───────────────────────────────────────────────────────

    /// <summary>
    /// GET /audit/logs?limit=10&amp;offset=0 should return 200 with an array body (may be empty).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAuditLogs_ReturnsOk_WithArrayBody()
    {
        var response = await _client.GetAsync("/audit/logs?limit=10&offset=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<IEnumerable<JsonElement>>();
        Assert.NotNull(body); // may be empty, but must not be null
    }

    // ── POST /commands/execute ────────────────────────────────────────────────

    /// <summary>
    /// POST /commands/execute for device 27 must not return 400, 401, 404, or 500.
    /// Device may be online (200) or offline/timeout (503/504) — both are valid.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostCommandsExecute_Device27_ReturnsValidStatus()
    {
        var payload = new
        {
            deviceId = 27,
            command = "whoami",
            shell = "bash",
            timeoutSeconds = 15
        };

        using var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/commands/execute", content);

        // 200 = command executed, 503 = device/hub offline, 504 = command timed out.
        // All three are valid live-stack outcomes. The unacceptable codes are:
        // 400 (bad request — body is valid), 401 (auth failure — token is set),
        // 404 (device not found — 27 exists), 500 (unhandled exception).
        var acceptableStatuses = new[]
        {
            HttpStatusCode.OK,
            (HttpStatusCode)503,
            (HttpStatusCode)504,
        };

        Assert.Contains(response.StatusCode, acceptableStatuses);
    }

    // ── Auth guard spot-checks ────────────────────────────────────────────────

    /// <summary>
    /// GET /dashboard without a Bearer token must return 401 Unauthorized.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetDashboard_WithoutToken_Returns401()
    {
        var unauthClient = _factory.CreateClient();

        var response = await unauthClient.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// GET /devices without a Bearer token must return 401 Unauthorized.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetDevices_WithoutToken_Returns401()
    {
        var unauthClient = _factory.CreateClient();

        var response = await unauthClient.GetAsync("/devices?page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// GET /tenants with a TenantMember (ClientAdmin) JWT should return 200.
    /// ClientAdmin is a valid TenantMember when a tenant_id claim is present.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetTenants_WithClientAdminToken_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var token = _factory.IssueToken(Role.ClientAdmin, tenantId: 1);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/tenants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// GET /audit/logs with a Technician JWT should return 200.
    /// Technician is allowed by the TenantMember policy when tenant_id is present.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAuditLogs_WithTechnicianToken_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var token = _factory.IssueToken(Role.Technician, tenantId: 1);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/audit/logs?limit=5&offset=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
