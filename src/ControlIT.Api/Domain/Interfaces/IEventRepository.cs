// ─────────────────────────────────────────────────────────────────────────────
// IEventRepository.cs
// Pattern: Repository — same idea as IDeviceRepository; isolates event table
// SQL behind a stable interface.
//
// WHY: The events table has a non-standard column (`_event`) and a date column
// named `date` (not `timestamp`). All the SQL aliasing complexity lives in
// MySqlEventRepository — callers just see clean DeviceEvent objects.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Domain.Interfaces;

using ControlIT.Api.Application;
using ControlIT.Api.Domain.Models;

public interface IEventRepository
{
    // Returns paginated events filtered to the caller's tenant.
    // The events table joins to tenants via tenant_name — the repository handles this.
    Task<(IEnumerable<DeviceEvent> Items, int TotalCount)> GetAllAsync(
        TenantContext tenantContext, int limit, int offset,
        CancellationToken cancellationToken = default);

    // Separate count query — used by the dashboard summary.
    Task<int> GetTotalCountAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);
}
