namespace ControlIT.Api.Domain.Interfaces;

using ControlIT.Api.Domain.Models;

public interface INetbirdMappingRepository
{
    Task<DeviceNetbirdMap?> GetByDeviceIdAsync(int deviceId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, DeviceNetbirdMap>> GetByDeviceIdsAsync(
        IEnumerable<int> deviceIds,
        CancellationToken ct = default);
    Task<DeviceNetbirdMap?> GetByPeerIdAsync(string peerId, CancellationToken ct = default);
    Task CreateMappingAsync(DeviceNetbirdMap map, CancellationToken ct = default);
    Task DeleteByDeviceIdAsync(int deviceId, CancellationToken ct = default);
    Task DeleteByPeerIdAsync(string peerId, CancellationToken ct = default);

    Task<TenantNetbirdGroup?> GetTenantGroupAsync(int tenantId, CancellationToken ct = default);
    Task<TenantNetbirdGroup?> GetTenantGroupByNetbirdGroupIdAsync(string groupId, CancellationToken ct = default);
    Task CreateTenantGroupAsync(TenantNetbirdGroup group, CancellationToken ct = default);
    Task UpsertTenantGroupAsync(TenantNetbirdGroup group, CancellationToken ct = default);
    Task DeleteTenantGroupAsync(int tenantId, CancellationToken ct = default);
}
