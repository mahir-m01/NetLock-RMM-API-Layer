namespace ControlIT.Api.Infrastructure.Persistence;

using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.EntityFrameworkCore;

public class NetbirdMappingRepository : INetbirdMappingRepository
{
    private readonly ControlItDbContext _db;

    public NetbirdMappingRepository(ControlItDbContext db) => _db = db;

    public async Task<DeviceNetbirdMap?> GetByDeviceIdAsync(int deviceId, CancellationToken ct = default)
        => await _db.DeviceNetbirdMaps.FirstOrDefaultAsync(m => m.DeviceId == deviceId, ct);

    public async Task<IReadOnlyDictionary<int, DeviceNetbirdMap>> GetByDeviceIdsAsync(
        IEnumerable<int> deviceIds,
        CancellationToken ct = default)
    {
        var ids = deviceIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<int, DeviceNetbirdMap>();

        return await _db.DeviceNetbirdMaps
            .Where(m => ids.Contains(m.DeviceId))
            .ToDictionaryAsync(m => m.DeviceId, ct);
    }

    public async Task<DeviceNetbirdMap?> GetByPeerIdAsync(string peerId, CancellationToken ct = default)
        => await _db.DeviceNetbirdMaps.FirstOrDefaultAsync(m => m.NetbirdPeerId == peerId, ct);

    public async Task CreateMappingAsync(DeviceNetbirdMap map, CancellationToken ct = default)
    {
        _db.DeviceNetbirdMaps.Add(map);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteByDeviceIdAsync(int deviceId, CancellationToken ct = default)
    {
        var map = await _db.DeviceNetbirdMaps.FirstOrDefaultAsync(m => m.DeviceId == deviceId, ct);
        if (map is not null)
        {
            _db.DeviceNetbirdMaps.Remove(map);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteByPeerIdAsync(string peerId, CancellationToken ct = default)
    {
        var map = await _db.DeviceNetbirdMaps.FirstOrDefaultAsync(m => m.NetbirdPeerId == peerId, ct);
        if (map is not null)
        {
            _db.DeviceNetbirdMaps.Remove(map);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<TenantNetbirdGroup?> GetTenantGroupAsync(int tenantId, CancellationToken ct = default)
        => await _db.TenantNetbirdGroups.FirstOrDefaultAsync(g => g.TenantId == tenantId, ct);

    public async Task<TenantNetbirdGroup?> GetTenantGroupByNetbirdGroupIdAsync(
        string groupId,
        CancellationToken ct = default)
        => await _db.TenantNetbirdGroups.FirstOrDefaultAsync(g => g.NetbirdGroupId == groupId, ct);

    public async Task CreateTenantGroupAsync(TenantNetbirdGroup group, CancellationToken ct = default)
    {
        _db.TenantNetbirdGroups.Add(group);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpsertTenantGroupAsync(TenantNetbirdGroup group, CancellationToken ct = default)
    {
        var existing = await _db.TenantNetbirdGroups.FirstOrDefaultAsync(
            g => g.TenantId == group.TenantId, ct);

        if (existing is null)
        {
            _db.TenantNetbirdGroups.Add(group);
        }
        else
        {
            existing.NetbirdGroupId = group.NetbirdGroupId;
            existing.NetbirdGroupName = group.NetbirdGroupName;
            existing.IsolationPolicyId = group.IsolationPolicyId;
            existing.GroupMode = group.GroupMode;
            existing.ControlItManaged = group.ControlItManaged;
            existing.UpdatedAt = group.UpdatedAt;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteTenantGroupAsync(int tenantId, CancellationToken ct = default)
    {
        var group = await _db.TenantNetbirdGroups.FirstOrDefaultAsync(g => g.TenantId == tenantId, ct);
        if (group is not null)
        {
            _db.TenantNetbirdGroups.Remove(group);
            await _db.SaveChangesAsync(ct);
        }
    }
}
