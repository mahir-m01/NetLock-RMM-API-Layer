// ─────────────────────────────────────────────────────────────────────────────
// IAuditService.cs
// Pattern: Service — encapsulates audit log write/query logic behind an
// interface, separating it from the repository (which does raw SQL).
//
// WHY: RecordAsync has a special contract — it must NEVER throw. If an audit
// write fails, the user operation continues with a Critical log entry. This
// "fire and forget with error logging" behaviour is enforced here by
// AuditService.RecordAsync catching all exceptions internally.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Domain.Interfaces;

using ControlIT.Api.Domain.DTOs.Responses;
using ControlIT.Api.Domain.Models;

public interface IAuditService
{
    /// <summary>
    /// Records an audit event. Must be called before the operation executes.
    /// Never throws — if the write fails, log at Critical and continue.
    /// This is intentional: a broken audit sink must never block a user operation.
    /// </summary>
    Task RecordAsync(AuditEntry entry);

    // Query audit entries for a tenant, optionally filtered by date range.
    // limit/offset provide pagination consistent with the rest of the API.
    // null tenantId = all tenants (SuperAdmin/CpAdmin); scoped users see their tenant only.
    // Returns AuditLogResponse DTOs — never the raw domain entity.
    Task<IEnumerable<AuditLogResponse>> QueryAsync(
        int? tenantId, DateTime? from, DateTime? to, int limit, int offset);
}
