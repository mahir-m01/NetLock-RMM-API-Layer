// ─────────────────────────────────────────────────────────────────────────────
// NetworkTenantTargetTests.cs
// Integration tests for the TenantTargetResolver behaviour on network endpoints.
//
// These tests verify that:
//   - Elevated roles (SuperAdmin, CpAdmin) get 400 when targetTenantId is absent
//   - Elevated roles get 400 when targetTenantId refers to a non-existent tenant
//   - Scoped roles get 403 when targetTenantId differs from their own tenantId
//   - Scoped roles are NOT rejected when targetTenantId matches their own tenantId
//
// The early-rejection assertions (400/403) never reach the Netbird layer, so
// they pass even without a live Netbird or full DB seed.
//
// Run with: dotnet test --filter "Category=Integration"
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ControlIT.Api.Application;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using ControlIT.Api.Tests.Fixtures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

[Collection("Database")]
public class NetworkTenantTargetTests
{
    private readonly ControlItWebApplicationFactory _factory;

    public NetworkTenantTargetTests(MySqlContainerFixture dbFixture)
    {
        _factory = new ControlItWebApplicationFactory
        {
            DatabaseConnectionString = dbFixture.ConnectionString
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient CreateClientWithToken(Role role, int? tenantId = null)
    {
        var client = _factory.CreateClient();
        var token = _factory.IssueToken(role, tenantId);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ── SuperAdmin/CpAdmin without targetTenantId → 400 ──────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ElevatedRole_NetworkPeers_WithoutTargetTenantId_Returns400()
    {
        var client = CreateClientWithToken(Role.SuperAdmin);

        var response = await client.GetAsync("/network/peers");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ElevatedRole_SetupKeys_WithoutTargetTenantId_Returns400()
    {
        var client = CreateClientWithToken(Role.CpAdmin);

        var response = await client.GetAsync("/network/setup-keys");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ElevatedRole_CreateSetupKey_WithoutTargetTenantId_Returns400()
    {
        var client = CreateClientWithToken(Role.SuperAdmin);

        var body = JsonSerializer.Serialize(new
        {
            name = "test-key",
            type = "reusable",
            expiresInDays = 30,
            usageLimit = 0,
            ephemeral = false,
        });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/network/setup-keys", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Elevated role with invalid (non-existent) targetTenantId → 400 ────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ElevatedRole_NetworkPeers_WithInvalidTargetTenantId_Returns400()
    {
        var client = CreateClientWithToken(Role.SuperAdmin);

        var response = await client.GetAsync("/network/peers?targetTenantId=999999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Scoped role with cross-tenant targetTenantId → 403 ───────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ScopedRole_NetworkPeers_WithDifferentTargetTenantId_Returns403()
    {
        // ClientAdmin belongs to tenant 1; requesting tenant 999 is cross-tenant.
        var client = CreateClientWithToken(Role.ClientAdmin, tenantId: 1);

        var response = await client.GetAsync("/network/peers?targetTenantId=999");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Scoped role with matching targetTenantId → NOT 403 ───────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ScopedRole_SetupKeys_WithMatchingTargetTenantId_DoesNotReturn403()
    {
        // ClientAdmin belongs to tenant 1; supplying the same tenant is allowed.
        // Downstream (Netbird) may not be available, so we only assert not-403.
        var client = CreateClientWithToken(Role.ClientAdmin, tenantId: 1);

        var response = await client.GetAsync("/network/setup-keys?targetTenantId=1");

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ElevatedRole_NetworkGroups_ReturnsNetbirdGroups()
    {
        var netbird = new Mock<INetbirdClient>(MockBehavior.Strict);
        netbird
            .Setup(n => n.GetGroupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new NetbirdGroup { Id = "group-a", Name = "Customer A" }
            ]);

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<INetbirdClient>();
                services.AddSingleton(netbird.Object);
            });
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.IssueToken(Role.CpAdmin));

        var response = await client.GetAsync("/network/groups");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("group-a", body);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task BindTenantGroup_WithExistingGroup_UpsertsExternalMapping()
    {
        const int tenantId = 5;
        const string groupId = "group-existing";
        TenantNetbirdGroup? captured = null;
        var netbird = new Mock<INetbirdClient>(MockBehavior.Strict);
        netbird
            .Setup(n => n.GetGroupByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NetbirdGroup { Id = groupId, Name = "Existing Customer Group" });

        var mappingRepo = new Mock<INetbirdMappingRepository>(MockBehavior.Strict);
        mappingRepo
            .Setup(r => r.GetTenantGroupByNetbirdGroupIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantNetbirdGroup?)null);
        mappingRepo
            .Setup(r => r.UpsertTenantGroupAsync(It.IsAny<TenantNetbirdGroup>(), It.IsAny<CancellationToken>()))
            .Callback<TenantNetbirdGroup, CancellationToken>((group, _) => captured = group)
            .Returns(Task.CompletedTask);

        var tenants = new Mock<ITenantRepository>(MockBehavior.Strict);
        tenants
            .Setup(t => t.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant { Id = tenantId, Guid = "tenant-a", Name = "Tenant A" });

        var audit = new Mock<IAuditService>(MockBehavior.Strict);
        audit
            .Setup(a => a.RecordAsync(It.IsAny<AuditEntry>()))
            .Returns(Task.CompletedTask);

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<INetbirdClient>();
                services.RemoveAll<INetbirdMappingRepository>();
                services.RemoveAll<ITenantRepository>();
                services.RemoveAll<IAuditService>();

                services.AddSingleton(netbird.Object);
                services.AddSingleton(mappingRepo.Object);
                services.AddSingleton(tenants.Object);
                services.AddSingleton(audit.Object);
            });
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.IssueToken(Role.CpAdmin));

        var response = await client.PostAsJsonAsync(
            $"/network/tenant-group?targetTenantId={tenantId}",
            new { GroupId = groupId, Mode = TenantNetbirdGroupMode.External });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(captured);
        Assert.Equal(groupId, captured!.NetbirdGroupId);
        Assert.Equal(TenantNetbirdGroupMode.External, captured.GroupMode);
        Assert.False(captured.ControlItManaged);
        audit.Verify(a => a.RecordAsync(It.IsAny<AuditEntry>()), Times.Exactly(2));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ScopedRole_BindTenantGroup_WithDifferentTargetTenantId_Returns403()
    {
        var client = CreateClientWithToken(Role.ClientAdmin, tenantId: 1);

        var response = await client.PostAsJsonAsync(
            "/network/tenant-group?targetTenantId=2",
            new { GroupId = "group-a", Mode = TenantNetbirdGroupMode.External });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task BindTenantGroup_WithManagedMode_Returns400_AndDoesNotCallNetbird()
    {
        const int tenantId = 5;
        var netbird = new Mock<INetbirdClient>(MockBehavior.Strict);
        var tenants = new Mock<ITenantRepository>(MockBehavior.Strict);
        tenants
            .Setup(t => t.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant { Id = tenantId, Guid = "tenant-a", Name = "Tenant A" });

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<INetbirdClient>();
                services.RemoveAll<ITenantRepository>();
                services.AddSingleton(netbird.Object);
                services.AddSingleton(tenants.Object);
            });
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.IssueToken(Role.CpAdmin));

        var response = await client.PostAsJsonAsync(
            $"/network/tenant-group?targetTenantId={tenantId}",
            new { GroupId = "group-a", Mode = TenantNetbirdGroupMode.Managed });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        netbird.Verify(n => n.GetGroupByIdAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LinkPeer_WithDeviceOutsideResolvedTenant_Returns404_AndDoesNotCreateMapping()
    {
        const int tenantId = 5;
        const int deviceId = 123;
        const string peerId = "peer-tenant-a";
        const string groupId = "group-tenant-a";

        var netbird = new Mock<INetbirdClient>(MockBehavior.Strict);
        netbird
            .Setup(n => n.GetPeerByIdAsync(peerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NetbirdPeer
            {
                Id = peerId,
                Ip = "100.64.0.10",
                Hostname = "tenant-a-peer",
                Groups = [new NetbirdPeerGroup { Id = groupId, Name = "tenant-a" }]
            });

        var mappingRepo = new Mock<INetbirdMappingRepository>(MockBehavior.Strict);
        mappingRepo
            .Setup(r => r.GetTenantGroupAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantNetbirdGroup
            {
                TenantId = tenantId,
                NetbirdGroupId = groupId,
                NetbirdGroupName = "controlit-tenant-5",
                IsolationPolicyId = "policy-tenant-a"
            });

        var devices = new Mock<IDeviceRepository>(MockBehavior.Strict);
        devices
            .Setup(d => d.GetByIdAsync(deviceId, It.IsAny<TenantContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Device?)null);

        var tenants = new Mock<ITenantRepository>(MockBehavior.Strict);
        tenants
            .Setup(t => t.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant { Id = tenantId, Guid = "tenant-a", Name = "Tenant A" });

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<INetbirdClient>();
                services.RemoveAll<INetbirdMappingRepository>();
                services.RemoveAll<IDeviceRepository>();
                services.RemoveAll<ITenantRepository>();

                services.AddSingleton(netbird.Object);
                services.AddSingleton(mappingRepo.Object);
                services.AddSingleton(devices.Object);
                services.AddSingleton(tenants.Object);
            });
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.IssueToken(Role.CpAdmin));

        var response = await client.PostAsJsonAsync(
            $"/network/peers/{peerId}/link?targetTenantId={tenantId}",
            new { DeviceId = deviceId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        mappingRepo.Verify(
            r => r.CreateMappingAsync(It.IsAny<DeviceNetbirdMap>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
