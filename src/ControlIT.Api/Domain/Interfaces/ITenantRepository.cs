// ─────────────────────────────────────────────────────────────────────────────
// ITenantRepository.cs
// Pattern: Repository — abstracts tenant and location data access.
//
// WHY: Tenant data is read-only from ControlIT's perspective (NetLock owns
// those tables). The interface keeps the read path clean and testable.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Domain.Interfaces;

using ControlIT.Api.Domain.Models;

public interface ITenantRepository
{
    // Returns all tenants. Phase 1 is single-tenant but the interface supports multi.
    Task<IEnumerable<Tenant>> GetAllAsync(CancellationToken cancellationToken = default);

    // Returns a tenant with its locations pre-populated via QueryMultipleAsync.
    // Returns null if not found — never throws a "not found" exception.
    Task<Tenant?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    // Returns locations for a specific tenant. Separate method for the
    // GET /tenants/{id}/locations endpoint.
    Task<IEnumerable<Location>> GetLocationsByTenantAsync(
        int tenantId, CancellationToken cancellationToken = default);
}
