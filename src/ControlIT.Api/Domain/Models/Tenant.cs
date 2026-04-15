// Tenant.cs — Domain model representing an organization/customer in NetLock's database.
// Mapped by Dapper from the `tenants` table.
//
// The Locations list is NOT a database column — it's populated via a second query.
// When you call MySqlTenantRepository.GetByIdAsync(), it fires two SQL statements
// (using QueryMultipleAsync) in a single round-trip: one for the tenant, one for its locations.

namespace ControlIT.Api.Domain.Models;

/// <summary>
/// Represents a tenant (organization/customer) from NetLock's `tenants` table.
/// Mapped by Dapper. Locations populated via a separate query on the locations table.
/// </summary>
public class Tenant
{
    // Primary key
    public int Id { get; set; }

    // UUID string that NetLock uses internally to identify tenants across systems
    public string Guid { get; set; } = string.Empty;

    // Display name of the tenant/organization
    public string Name { get; set; } = string.Empty;

    // Not a database column — populated via QueryMultipleAsync in the repository.
    public List<Location> Locations { get; set; } = [];
}
