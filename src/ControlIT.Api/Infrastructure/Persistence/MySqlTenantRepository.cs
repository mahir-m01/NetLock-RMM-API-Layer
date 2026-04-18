// ─────────────────────────────────────────────────────────────────────────────
// MySqlTenantRepository.cs
// Pattern: Repository (concrete) — all tenant and location SQL lives here.
//
// WHY QueryMultipleAsync for GetByIdAsync: The Tenant model has a Locations list.
// Rather than two separate round-trips (one for tenant, one for locations), we
// send both queries in a single DB call using a multi-result reader.
// This is the "N+1 query" prevention pattern — one SQL call, two result sets.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Infrastructure.Persistence;

using Dapper;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;

public class MySqlTenantRepository : ITenantRepository
{
    private readonly IDbConnectionFactory _factory;

    public MySqlTenantRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<Tenant>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        using var conn = await _factory.CreateConnectionAsync(cancellationToken);

        // No tenant scoping — this is an admin-level endpoint.
        // Phase 2: add role-based auth check.
        return await conn.QueryAsync<Tenant>(
            "SELECT id, guid, name FROM tenants ORDER BY name");
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        using var conn = await _factory.CreateConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM tenants");
    }

    public async Task<Tenant?> GetByIdAsync(int id,
        CancellationToken cancellationToken = default)
    {
        using var conn = await _factory.CreateConnectionAsync(cancellationToken);

        // Two SQL statements in a single round-trip — MySQL returns two result sets.
        using var multi = await conn.QueryMultipleAsync(
            """
            SELECT id, guid, name FROM tenants WHERE id = @id;
            SELECT id, tenant_id, guid, name FROM locations WHERE tenant_id = @id;
            """,
            new { id });

        var tenant = await multi.ReadFirstOrDefaultAsync<Tenant>();
        if (tenant is null) return null;

        tenant.Locations = (await multi.ReadAsync<Location>()).ToList();
        return tenant;
    }

    public async Task<IEnumerable<Location>> GetLocationsByTenantAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        using var conn = await _factory.CreateConnectionAsync(cancellationToken);

        return await conn.QueryAsync<Location>(
            "SELECT id, tenant_id, guid, name FROM locations WHERE tenant_id = @tenantId",
            new { tenantId });
    }
}
