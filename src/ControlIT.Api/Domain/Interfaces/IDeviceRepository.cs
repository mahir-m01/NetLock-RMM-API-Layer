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

// In C#, interfaces define a "contract" — similar to TypeScript interfaces, but they're
// also used for Dependency Injection so we can swap implementations without changing callers.
public interface IDeviceRepository
{
    // Returns a paginated list of devices. TenantContext is like a "session variable"
    // that was set by the middleware — it tells us which tenant this request belongs to.
    // The tuple return `(IEnumerable<Device> Items, int TotalCount)` is C#'s way of returning
    // multiple values — like `{ items, total }` destructuring in JavaScript.
    Task<(IEnumerable<Device> Items, int TotalCount)> GetAllAsync(
        DeviceFilter filter, TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    // Null return (`Device?`) means "not found". The `?` is C#'s nullable reference type —
    // similar to `Device | null` in TypeScript.
    Task<Device?> GetByIdAsync(
        int id, TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    // Returns the real-time count of online devices. Uses last_access threshold.
    // MUST be a real COUNT query — never return a hardcoded placeholder.
    Task<int> GetOnlineCountAsync(
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
