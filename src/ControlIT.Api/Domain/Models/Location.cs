// Location.cs — Domain model representing a physical or logical grouping of devices.
// Mapped by Dapper from the `locations` table.
// Examples: "New York Office", "Data Center East", "Remote Workers"

namespace ControlIT.Api.Domain.Models;

/// <summary>
/// Represents a device location from NetLock's `locations` table.
/// Mapped by Dapper. Always owned by exactly one tenant (tenant_id FK).
/// </summary>
public class Location
{
    // Primary key
    public int Id { get; set; }

    // Foreign key to the tenants table — which org owns this location
    public int TenantId { get; set; }

    // UUID string used by NetLock internally
    public string Guid { get; set; } = string.Empty;

    // Human-readable name (e.g., "HQ London", "Cloud DC")
    public string Name { get; set; } = string.Empty;
}
