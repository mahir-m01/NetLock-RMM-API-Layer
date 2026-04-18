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

    // Inserts a single audit entry. AuditService.RecordAsync wraps this in a
    // try/catch, so throwing on DB failure is correct here.
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
            entry);
    }

    // Queries the audit log with optional date range filtering and pagination.
    public async Task<IEnumerable<AuditEntry>> QueryAsync(
        int? tenantId, DateTime? from, DateTime? to, int limit, int offset)
    {
        using var conn = await _factory.CreateConnectionAsync();

        var conditions = new List<string>();
        var p = new DynamicParameters();

        // null tenantId = SuperAdmin/CpAdmin; no tenant filter applied.
        if (tenantId.HasValue)
        {
            conditions.Add("tenant_id = @tenantId");
            p.Add("tenantId", tenantId.Value);
        }

        if (from.HasValue) { conditions.Add("timestamp >= @from"); p.Add("from", from); }
        if (to.HasValue) { conditions.Add("timestamp <= @to"); p.Add("to", to); }

        p.Add("limit", limit);
        p.Add("offset", offset);

        var where = conditions.Count > 0 ? string.Join(" AND ", conditions) : "1=1";
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
