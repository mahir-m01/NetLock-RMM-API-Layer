namespace ControlIT.Api.Tests.Unit;

using ControlIT.Api.Application;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

[Trait("Category", "Unit")]
public class TenantScopedDeviceGuardTests
{
    [Fact]
    public async Task ExistsInTenantAsync_UsesScopedTenantContext()
    {
        var devices = new Mock<IDeviceRepository>();
        TenantContext? captured = null;
        devices
            .Setup(d => d.GetByIdAsync(27, It.IsAny<TenantContext>(), default))
            .Callback<int, TenantContext, CancellationToken>((_, ctx, _) => captured = ctx)
            .ReturnsAsync(new Device { Id = 27, TenantId = 5 });

        var exists = await TenantScopedDeviceGuard.ExistsInTenantAsync(devices.Object, 27, 5);

        Assert.True(exists);
        Assert.NotNull(captured);
        Assert.True(captured!.IsResolved);
        Assert.False(captured.IsAllTenants);
        Assert.Equal(5, captured.TenantId);
    }

    [Fact]
    public async Task ExistsInTenantAsync_ReturnsFalse_WhenRepositoryCannotSeeDevice()
    {
        var devices = new Mock<IDeviceRepository>();
        devices
            .Setup(d => d.GetByIdAsync(99, It.IsAny<TenantContext>(), default))
            .ReturnsAsync((Device?)null);

        var exists = await TenantScopedDeviceGuard.ExistsInTenantAsync(devices.Object, 99, 5);

        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsInTenantAsync_PassesRequestedDeviceId()
    {
        var devices = new Mock<IDeviceRepository>();
        devices
            .Setup(d => d.GetByIdAsync(123, It.IsAny<TenantContext>(), default))
            .ReturnsAsync(new Device { Id = 123, TenantId = 8 });

        var exists = await TenantScopedDeviceGuard.ExistsInTenantAsync(devices.Object, 123, 8);

        Assert.True(exists);
        devices.Verify(d => d.GetByIdAsync(123, It.IsAny<TenantContext>(), default), Times.Once);
    }

    [Fact]
    public async Task LinkAsync_Returns404_WhenPeerBelongsToTargetTenantButDeviceOutsideScope_AndDoesNotMapOrAudit()
    {
        const int tenantId = 5;
        const int deviceId = 123;
        const string peerId = "peer-tenant-a";
        const string groupId = "group-tenant-a";

        var netbird = new Mock<INetbirdClient>(MockBehavior.Strict);
        netbird
            .Setup(n => n.GetPeerByIdAsync(peerId, default))
            .ReturnsAsync(new NetbirdPeer
            {
                Id = peerId,
                Ip = "100.64.0.10",
                Hostname = "tenant-a-peer",
                Groups = [new NetbirdPeerGroup { Id = groupId, Name = "tenant-a" }]
            });

        var mappingRepo = new Mock<INetbirdMappingRepository>(MockBehavior.Strict);
        mappingRepo
            .Setup(r => r.GetTenantGroupAsync(tenantId, default))
            .ReturnsAsync(new TenantNetbirdGroup
            {
                TenantId = tenantId,
                NetbirdGroupId = groupId,
                NetbirdGroupName = "controlit-tenant-5",
                IsolationPolicyId = "policy-tenant-a"
            });

        TenantContext? capturedTenantContext = null;
        var devices = new Mock<IDeviceRepository>(MockBehavior.Strict);
        devices
            .Setup(d => d.GetByIdAsync(deviceId, It.IsAny<TenantContext>(), default))
            .Callback<int, TenantContext, CancellationToken>((_, ctx, _) => capturedTenantContext = ctx)
            .ReturnsAsync((Device?)null);

        var audit = new Mock<IAuditService>(MockBehavior.Strict);
        var actor = new Mock<IActorContext>(MockBehavior.Strict);
        var networkService = new TenantNetworkService(
            netbird.Object,
            mappingRepo.Object,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<TenantNetworkService>.Instance);

        var result = await PeerDeviceLinkHandler.LinkAsync(
            peerId,
            deviceId,
            netbird.Object,
            mappingRepo.Object,
            devices.Object,
            networkService,
            audit.Object,
            actor.Object,
            tenantId);

        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, status.StatusCode);
        Assert.NotNull(capturedTenantContext);
        Assert.Equal(tenantId, capturedTenantContext!.TenantId);
        Assert.False(capturedTenantContext.IsAllTenants);
        mappingRepo.Verify(r => r.GetByDeviceIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        mappingRepo.Verify(r => r.GetByPeerIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        mappingRepo.Verify(r => r.CreateMappingAsync(It.IsAny<DeviceNetbirdMap>(), It.IsAny<CancellationToken>()), Times.Never);
        audit.Verify(a => a.RecordAsync(It.IsAny<AuditEntry>()), Times.Never);
    }
}
