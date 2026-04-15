// ─────────────────────────────────────────────────────────────────────────────
// AuditRepository.cs
// Pattern: Repository — raw Dapper SQL for controlit_audit_log writes/reads.
//
// WHY Dapper for audit writes (not EF Core): Audit writes happen inside
// exception handlers and critical paths. Dapper's lightweight execution is
// more reliable under error conditions than EF Core's change tracker.
// Also, audit writes must never throw — if the connection is bad, we catch
// in AuditService and log Critical rather than propagating the error.
//
// This class is registered Scoped (not Singleton) — it holds no state,
// just calls the factory on each method invocation.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Infrastructure.Persistence;

using Dapper;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;

public class AuditRepository
{
    private readonly IDbConnectionFactory _factory;

    public AuditRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    // Inserts a single audit entry. Called by AuditService.RecordAsync which
    // wraps this in a try/catch — so this method can throw on DB failure.
    public async Task InsertAsync(AuditEntry entry)
    {
        using var conn = await _factory.CreateConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO controlit_audit_log
                (timestamp, tenant_id, actor_key_id, action, resource_type,
                 resource_id, ip_address, result, error_message)
            VALUES
                (@Timestamp, @TenantId, @ActorKeyId, @Action, @ResourceType,
                 @ResourceId, @IpAddress, @Result, @ErrorMessage)
            """,
            entry);  // Dapper maps entry's public properties to @Parameters by name
    }

    // Queries audit log with optional date range filtering and pagination.
    // tenantId comes from TenantContext — never from a request parameter.
    public async Task<IEnumerable<AuditEntry>> QueryAsync(
        int tenantId, DateTime? from, DateTime? to, int limit, int offset)
    {
        using var conn = await _factory.CreateConnectionAsync();

        // DynamicParameters allows building the WHERE clause conditionally —
        // only add date conditions if the caller provided them.
        var conditions = new List<string> { "tenant_id = @tenantId" };
        var p = new DynamicParameters();
        p.Add("tenantId", tenantId);

        if (from.HasValue) { conditions.Add("timestamp >= @from"); p.Add("from", from); }
        if (to.HasValue) { conditions.Add("timestamp <= @to"); p.Add("to", to); }

        p.Add("limit", limit);
        p.Add("offset", offset);

        var where = string.Join(" AND ", conditions);
        return await conn.QueryAsync<AuditEntry>(
            $"""
            SELECT id, timestamp, tenant_id, actor_key_id, action, resource_type,
                   resource_id, ip_address, result, error_message
            FROM controlit_audit_log
            WHERE {where}
            ORDER BY timestamp DESC
            LIMIT @limit OFFSET @offset
            """, p);
    }
}
