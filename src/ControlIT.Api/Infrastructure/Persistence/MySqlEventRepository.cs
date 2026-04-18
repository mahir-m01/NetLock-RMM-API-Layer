// ─────────────────────────────────────────────────────────────────────────────
// MySqlEventRepository.cs
// Pattern: Repository (concrete) — all event table SQL lives here.
//
// CRITICAL — Two SQL aliasing rules that MUST appear in every query:
//   1. `e._event AS Event` — the column name `_event` has a leading underscore.
//      MatchNamesWithUnderscores treats underscores as word separators, so it
//      would try to map `_event` to a property named `Event` but the leading
//      underscore is NOT a separator — it's part of the column name. Without
//      the explicit alias, Dapper maps this to null silently.
//   2. `e.date AS Timestamp` — the column is named `date`, not `timestamp`.
//      The DeviceEvent.Timestamp property only maps if we alias it.
//
// WHY JOIN to tenants: The events table does not have a tenant_id column —
// it has a tenant_name column. To filter by tenant, we must JOIN to the
// tenants table and match on name.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Infrastructure.Persistence;

using Dapper;
using ControlIT.Api.Application;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;

public class MySqlEventRepository : IEventRepository
{
    private readonly IDbConnectionFactory _factory;

    public MySqlEventRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<(IEnumerable<DeviceEvent> Items, int TotalCount)> GetAllAsync(
        TenantContext tenantContext, int limit, int offset,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.IsResolved)
            throw new InvalidOperationException("TenantContext not resolved before repository access.");

        using var conn = await _factory.CreateConnectionAsync(cancellationToken);

        // Both column aliases are required on every query touching these columns:
        //   e.date AS Timestamp  — maps the `date` column to DeviceEvent.Timestamp
        //   e._event AS Event    — maps the `_event` column to DeviceEvent.Event
        // The tenant column is tenant_name_snapshot (verified against NetLock's Event_Handler.cs).
        // The JOIN filters by tenant via the tenants table since events has no tenant_id column.
        // LEFT JOIN so events with an unresolvable tenant_name_snapshot still appear
        // for SuperAdmin (IsAllTenants=true). For tenanted users the WHERE t.id = @tenantId
        // clause still filters correctly because NULL != tenantId.
        var tenantFilter = tenantContext.IsAllTenants ? "" : "WHERE t.id = @tenantId";
        var sql = $"""
            SELECT SQL_CALC_FOUND_ROWS
                e.id, e.device_id, e.tenant_name_snapshot AS TenantName, e.device_name,
                e.date AS Timestamp,
                e.severity, e.reported_by,
                e._event AS Event,
                e.description
            FROM events e
            LEFT JOIN tenants t ON t.name = e.tenant_name_snapshot
            {tenantFilter}
            ORDER BY e.date DESC
            LIMIT @limit OFFSET @offset;
            SELECT FOUND_ROWS();
            """;

        using var multi = await conn.QueryMultipleAsync(sql,
            new { tenantId = tenantContext.TenantId, limit, offset });

        var items = await multi.ReadAsync<DeviceEvent>();
        var total = await multi.ReadSingleAsync<int>();

        return (items, total);
    }

    public async Task<int> GetTotalCountAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.IsResolved)
            throw new InvalidOperationException("TenantContext not resolved before repository access.");

        using var conn = await _factory.CreateConnectionAsync(cancellationToken);

        var tenantFilter = tenantContext.IsAllTenants ? "" : "WHERE t.id = @tenantId";
        return await conn.ExecuteScalarAsync<int>(
            $"""
            SELECT COUNT(*)
            FROM events e
            LEFT JOIN tenants t ON t.name = e.tenant_name_snapshot
            {tenantFilter}
            """,
            new { tenantId = tenantContext.TenantId });
    }
}
