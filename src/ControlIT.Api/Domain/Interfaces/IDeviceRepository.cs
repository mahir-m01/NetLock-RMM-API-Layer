// ─────────────────────────────────────────────────────────────────────────────
// IDeviceRepository.cs
// Pattern: Repository — abstracts data access behind a clean interface so the
// application layer doesn't know (or care) whether data comes from MySQL,
// an in-memory cache, or a mock for tests.
//
// WHY: If you inject IDeviceRepository, you can swap MySqlDeviceRepository for
// a fake in unit tests without changing any endpoint or facade code.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Domain.Interfaces;

using ControlIT.Api.Application;
using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.Models;

public interface IDeviceRepository
{
    // Returns a paginated list of devices filtered to the given tenant.
    Task<(IEnumerable<Device> Items, int TotalCount)> GetAllAsync(
        DeviceFilter filter, TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    // Returns null when the device is not found in the tenant's scope.
    Task<Device?> GetByIdAsync(
        int id, TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    // Returns all access_keys for the tenant's devices.
    // Used by ControlItFacade to intersect with NetLock's live connected-device list.
    Task<IEnumerable<string>> GetAllAccessKeysAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the device's access_key for SignalR command dispatch.
    /// Enforces tenant ownership check — returns null if device doesn't belong to this tenant.
    /// </summary>
    Task<string?> GetAccessKeyAsync(
        int deviceId, TenantContext tenantContext,
        CancellationToken cancellationToken = default);
}
