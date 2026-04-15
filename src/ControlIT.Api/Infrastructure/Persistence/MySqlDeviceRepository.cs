// ─────────────────────────────────────────────────────────────────────────────
// MySqlDeviceRepository.cs
// Pattern: Repository (concrete) — all device table SQL lives here.
// Implements IDeviceRepository using Dapper for the NetLock `devices` table.
//
// WHY Dapper (not EF Core): The devices table is owned by NetLock — we must
// never run EF migrations against it. Dapper gives us explicit SQL control
// with automatic mapping to C# properties via MatchNamesWithUnderscores.
//
// Tenant scoping: EVERY query includes `WHERE tenant_id = @tenantId` as the
// FIRST condition. This is a security boundary — never query without it.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Infrastructure.Persistence;

using Dapper;
using ControlIT.Api.Application;
using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;

public class MySqlDeviceRepository : IDeviceRepository
{
    private readonly IDbConnectionFactory _factory;

    // Constructor injection — the DI container provides IDbConnectionFactory.
    // This is the Dependency Inversion Principle: depend on the abstraction (interface),
    // not the concrete class (MySqlConnectionFactory).
    public MySqlDeviceRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<(IEnumerable<Device> Items, int TotalCount)> GetAllAsync(
        DeviceFilter filter, TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        // Guard: TenantContext must be resolved (set by ApiKeyMiddleware) before any query.
        // If IsResolved is false, the middleware didn't authenticate — this is a bug, not
        // a user error. Throw rather than silently returning data for the wrong tenant.
        if (!tenantContext.IsResolved)
            throw new InvalidOperationException("TenantContext not resolved before repository access.");

        using var conn = await _factory.CreateConnectionAsync(cancellationToken);

        // DynamicParameters is Dapper's way of building parameterized queries dynamically.
        // Parameters are ALWAYS used (never string interpolation) to prevent SQL injection.
        var conditions = new List<string> { "d.tenant_id = @tenantId" };
        var p = new DynamicParameters();
        p.Add("tenantId", tenantContext.TenantId);

        // Conditionally add filters — only if the caller provided them.
        if (!string.IsNullOrWhiteSpace(filter.Platform))
        {
            conditions.Add("d.platform = @platform");
            p.Add("platform", filter.Platform);
        }

        // OnlineOnly: filter to devices that checked in within the last 5 minutes.
        // Uses a server-side NOW() comparison — not hardcoded DateTime — so it's always accurate.
        if (filter.OnlineOnly == true)
        {
            conditions.Add("d.last_access >= DATE_SUB(NOW(), INTERVAL 5 MINUTE)");
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            conditions.Add("d.device_name LIKE @search");
            p.Add("search", $"%{filter.SearchTerm}%");
        }

        var where = string.Join(" AND ", conditions);
        var offset = (filter.Page - 1) * filter.PageSize;

        p.Add("pageSize", filter.PageSize);
        p.Add("offset", offset);

        // SQL_CALC_FOUND_ROWS + FOUND_ROWS() is MySQL's way of getting the total count
        // in the same query as the paginated results. Avoids a second COUNT(*) round-trip.
        // QueryMultipleAsync executes both SELECT statements in one DB call.
        var sql = $"""
            SELECT SQL_CALC_FOUND_ROWS
                d.id, d.tenant_id, d.location_id, d.device_name, d.access_key,
                d.platform, d.operating_system, d.agent_version,
                d.cpu, d.cpu_usage, d.ram, d.ram_usage,
                d.ip_address_internal, d.ip_address_external,
                d.last_access, d.authorized, d.synced
            FROM devices d
            WHERE {where}
            ORDER BY d.device_name
            LIMIT @pageSize OFFSET @offset;
            SELECT FOUND_ROWS();
            """;

        // QueryMultipleAsync returns a "grid reader" — read each result set in order.
        // ReadAsync<Device>() maps the first result set to Device objects.
        // ReadSingleAsync<int>() reads the single integer from FOUND_ROWS().
        using var multi = await conn.QueryMultipleAsync(sql, p);
        var items = await multi.ReadAsync<Device>();
        var total = await multi.ReadSingleAsync<int>();

        return (items, total);
    }

    public async Task<Device?> GetByIdAsync(
        int id, TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.IsResolved)
            throw new InvalidOperationException("TenantContext not resolved before repository access.");

        using var conn = await _factory.CreateConnectionAsync(cancellationToken);

        // QueryFirstOrDefaultAsync returns the first matching row, or null if none found.
        // The WHERE clause includes BOTH id AND tenant_id — this prevents a tenant from
        // reading another tenant's devices by guessing an ID.
        return await conn.QueryFirstOrDefaultAsync<Device>(
            """
            SELECT d.id, d.tenant_id, d.location_id, d.device_name, d.access_key,
                   d.platform, d.operating_system, d.agent_version,
                   d.cpu, d.cpu_usage, d.ram, d.ram_usage,
                   d.ip_address_internal, d.ip_address_external,
                   d.last_access, d.authorized, d.synced
            FROM devices d
            WHERE d.id = @id AND d.tenant_id = @tenantId
            """,
            new { id, tenantId = tenantContext.TenantId });
    }

    public async Task<int> GetOnlineCountAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.IsResolved)
            throw new InvalidOperationException("TenantContext not resolved before repository access.");

        using var conn = await _factory.CreateConnectionAsync(cancellationToken);

        // ExecuteScalarAsync<T> is Dapper's shorthand for queries that return a single scalar value.
        // last_access >= DATE_SUB(NOW(), INTERVAL 5 MINUTE) = "checked in within the last 5 minutes"
        // This is a real COUNT — NEVER hardcode -1 or any placeholder value here.
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM devices
            WHERE tenant_id = @tenantId
            AND last_access >= DATE_SUB(NOW(), INTERVAL 5 MINUTE)
            """,
            new { tenantId = tenantContext.TenantId });
    }

    public async Task<string?> GetAccessKeyAsync(
        int deviceId, TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.IsResolved)
            throw new InvalidOperationException("TenantContext not resolved before repository access.");

        using var conn = await _factory.CreateConnectionAsync(cancellationToken);

        // access_key is the NetLock identifier used for SignalR dispatch.
        // The tenant_id check ensures the caller can only get keys for their own devices.
        // Returns null (not an exception) if not found — callers handle the null case.
        return await conn.ExecuteScalarAsync<string?>(
            """
            SELECT access_key
            FROM devices
            WHERE id = @deviceId AND tenant_id = @tenantId
            """,
            new { deviceId, tenantId = tenantContext.TenantId });
    }
}
