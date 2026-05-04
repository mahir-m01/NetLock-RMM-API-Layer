namespace ControlIT.Api.Tests.Unit;

using ControlIT.Api.Application;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

[Trait("Category", "Unit")]
public class TenantNetworkServiceTests
{
    [Fact]
    public async Task BindTenantGroupAsync_ExternalMode_UpsertsExternalMapping()
    {
        const int tenantId = 7;
        const string groupId = "group-existing";
        TenantNetbirdGroup? captured = null;
        var netbird = new Mock<INetbirdClient>(MockBehavior.Strict);
        netbird
            .Setup(n => n.GetGroupByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NetbirdGroup { Id = groupId, Name = "Customer Existing" });

        var repo = new Mock<INetbirdMappingRepository>(MockBehavior.Strict);
        repo
            .Setup(r => r.GetTenantGroupByNetbirdGroupIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantNetbirdGroup?)null);
        repo
            .Setup(r => r.UpsertTenantGroupAsync(It.IsAny<TenantNetbirdGroup>(), It.IsAny<CancellationToken>()))
            .Callback<TenantNetbirdGroup, CancellationToken>((group, _) => captured = group)
            .Returns(Task.CompletedTask);

        var service = CreateService(netbird.Object, repo.Object);

        var result = await service.BindTenantGroupAsync(
            tenantId, groupId, TenantNetbirdGroupMode.External);

        Assert.Equal(groupId, result.NetbirdGroupId);
        Assert.Equal(TenantNetbirdGroupMode.External, captured!.GroupMode);
        Assert.False(captured.ControlItManaged);
        Assert.Equal(tenantId, captured.TenantId);
    }

    [Fact]
    public async Task GetTenantPeersAsync_UsesExternalMappedGroup()
    {
        const int tenantId = 4;
        const string groupId = "byo-group";
        var netbird = new Mock<INetbirdClient>(MockBehavior.Strict);
        netbird
            .Setup(n => n.GetPeersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new NetbirdPeer
                {
                    Id = "peer-visible",
                    Groups = [new NetbirdPeerGroup { Id = groupId, Name = "BYO" }]
                },
                new NetbirdPeer
                {
                    Id = "peer-hidden",
                    Groups = [new NetbirdPeerGroup { Id = "other", Name = "Other" }]
                }
            ]);

        var repo = new Mock<INetbirdMappingRepository>(MockBehavior.Strict);
        repo
            .Setup(r => r.GetTenantGroupAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantNetbirdGroup
            {
                TenantId = tenantId,
                NetbirdGroupId = groupId,
                NetbirdGroupName = "BYO",
                GroupMode = TenantNetbirdGroupMode.External,
                ControlItManaged = false
            });

        var service = CreateService(netbird.Object, repo.Object);

        var peers = (await service.GetTenantPeersAsync(tenantId)).ToList();

        Assert.Single(peers);
        Assert.Equal("peer-visible", peers[0].Id);
    }

    [Fact]
    public async Task BindTenantGroupAsync_ManagedMode_Throws()
    {
        var netbird = new Mock<INetbirdClient>(MockBehavior.Strict);
        var repo = new Mock<INetbirdMappingRepository>(MockBehavior.Strict);
        var service = CreateService(netbird.Object, repo.Object);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.BindTenantGroupAsync(1, "group-a", TenantNetbirdGroupMode.Managed));

        Assert.Contains("external or read_only", ex.Message);
        netbird.Verify(n => n.GetGroupByIdAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.UpsertTenantGroupAsync(
            It.IsAny<TenantNetbirdGroup>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureTenantGroupAsync_ExternalMissingGroup_DoesNotDeleteOrCreateOwnedResources()
    {
        const int tenantId = 9;
        const string groupId = "external-missing";
        var netbird = new Mock<INetbirdClient>(MockBehavior.Strict);
        netbird
            .Setup(n => n.GetGroupByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NetbirdGroup?)null);

        var repo = new Mock<INetbirdMappingRepository>(MockBehavior.Strict);
        repo
            .Setup(r => r.GetTenantGroupAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantNetbirdGroup
            {
                TenantId = tenantId,
                NetbirdGroupId = groupId,
                NetbirdGroupName = "External Missing",
                GroupMode = TenantNetbirdGroupMode.External,
                ControlItManaged = false
            });

        var service = CreateService(netbird.Object, repo.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.EnsureTenantGroupAsync(tenantId));
        repo.Verify(r => r.DeleteTenantGroupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        netbird.Verify(n => n.CreateGroupAsync(
            It.IsAny<string>(),
            It.IsAny<List<string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        netbird.Verify(n => n.CreatePolicyAsync(
            It.IsAny<ControlIT.Api.Domain.DTOs.Requests.CreatePolicyRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static TenantNetworkService CreateService(
        INetbirdClient netbird,
        INetbirdMappingRepository repo) =>
        new(
            netbird,
            repo,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<TenantNetworkService>.Instance);
}
