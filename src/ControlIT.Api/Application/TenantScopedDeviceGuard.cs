namespace ControlIT.Api.Application;

using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;

/// <summary>
/// Guards device operations that already resolved an explicit tenant target.
/// Elevated actors keep an all-tenant TenantContext, so use this helper when a
/// specific tenant id must be enforced after TenantTargetResolver succeeds.
/// </summary>
public static class TenantScopedDeviceGuard
{
    public static async Task<bool> ExistsInTenantAsync(
        IDeviceRepository devices,
        int deviceId,
        int tenantId,
        CancellationToken ct = default)
    {
        var tenantContext = new TenantContext(new ScopedTenantActorContext(tenantId));
        var device = await devices.GetByIdAsync(deviceId, tenantContext, ct);
        return device is not null;
    }

    private sealed class ScopedTenantActorContext(int tenantId) : IActorContext
    {
        public int UserId => 0;
        public Role Role => Role.ClientAdmin;
        public int? TenantId => tenantId;
        public IReadOnlyList<int> AssignedClients => [tenantId];
        public string? IpAddress => null;
        public string Email => "tenant-scope@controlit.local";
    }
}

public static class PeerDeviceLinkHandler
{
    public static async Task<IResult> LinkAsync(
        string peerId,
        int deviceId,
        INetbirdClient netbird,
        INetbirdMappingRepository mappingRepo,
        IDeviceRepository devices,
        TenantNetworkService networkService,
        IAuditService audit,
        IActorContext actor,
        int tenantId,
        CancellationToken ct = default)
    {
        var peer = await netbird.GetPeerByIdAsync(peerId, ct);
        if (peer is null)
            return Results.NotFound();

        var tenantGroup = await networkService.GetTenantGroupAsync(tenantId, ct);
        if (tenantGroup is null || !peer.Groups.Any(g => g.Id == tenantGroup.NetbirdGroupId))
            return Results.Forbid();

        var deviceVisibleInTenant = await TenantScopedDeviceGuard.ExistsInTenantAsync(
            devices,
            deviceId,
            tenantId,
            ct);
        if (!deviceVisibleInTenant)
            return Results.NotFound();

        var existingByDevice = await mappingRepo.GetByDeviceIdAsync(deviceId, ct);
        if (existingByDevice is not null)
            return Results.Conflict(new { error = "Device is already linked to a peer." });

        var existingByPeer = await mappingRepo.GetByPeerIdAsync(peerId, ct);
        if (existingByPeer is not null)
            return Results.Conflict(new { error = "Peer is already linked to a device." });

        await audit.RecordAsync(new AuditEntry
        {
            TenantId = tenantId,
            ActorKeyId = actor.UserId.ToString(),
            ActorEmail = actor.Email,
            Action = "DEVICE_PEER_LINK",
            ResourceType = "DeviceNetbirdMap",
            ResourceId = peerId,
            IpAddress = actor.IpAddress,
            Result = "PENDING"
        });

        await mappingRepo.CreateMappingAsync(new DeviceNetbirdMap
        {
            DeviceId = deviceId,
            NetbirdPeerId = peerId,
            NetbirdIp = peer.Ip,
            NetbirdHostname = peer.Hostname,
            MappedAt = DateTime.UtcNow,
            MappedBy = actor.Email
        }, ct);

        await audit.RecordAsync(new AuditEntry
        {
            TenantId = tenantId,
            ActorKeyId = actor.UserId.ToString(),
            ActorEmail = actor.Email,
            Action = "DEVICE_PEER_LINK",
            ResourceType = "DeviceNetbirdMap",
            ResourceId = peerId,
            IpAddress = actor.IpAddress,
            Result = "SUCCESS"
        });

        return Results.Ok();
    }
}
