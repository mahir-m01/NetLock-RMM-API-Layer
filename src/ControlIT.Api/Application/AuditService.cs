// ─────────────────────────────────────────────────────────────────────────────
// AuditService.cs
// Pattern: Service — implements IAuditService; sits between the endpoints
// and the AuditRepository.
//
// Critical contract: RecordAsync NEVER throws.
// WHY: Audit logging is a side-effect, not the primary operation. If the audit
// database is down, the user's command must still execute. A failed audit
// write is logged at Critical level (so it triggers alerts) but the exception
// is swallowed. "Fire and continue" semantics.
//
// This class is registered Scoped (one instance per HTTP request) because it
// depends on AuditRepository which is also Scoped.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Application;

using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using ControlIT.Api.Infrastructure.Persistence;

public class AuditService : IAuditService
{
    private readonly AuditRepository _repo;
    private readonly ILogger<AuditService> _logger;

    // Constructor injection — DI provides AuditRepository and ILogger.
    // Note: AuditRepository is registered directly (not behind an interface) because
    // it's an internal implementation detail — only AuditService uses it.
    public AuditService(AuditRepository repo, ILogger<AuditService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // The most important rule: this method must NEVER throw.
    // All exceptions are caught and logged at Critical level, then swallowed.
    // The caller continues executing regardless of whether the audit write succeeded.
    public async Task RecordAsync(AuditEntry entry)
    {
        try
        {
            await _repo.InsertAsync(entry);
        }
        catch (Exception ex)
        {
            // LogCritical — this is a serious issue that needs immediate attention.
            // In a production setup, Critical logs should trigger a PagerDuty alert.
            // We log the Action and TenantId so the on-call engineer knows WHAT failed.
            // We do NOT log sensitive data like the command content or API key.
            _logger.LogCritical(ex,
                "AUDIT WRITE FAILED for Action={Action} TenantId={TenantId}. " +
                "The operation will continue but this event has no audit trail.",
                entry.Action, entry.TenantId);
            // Exception is intentionally swallowed here — do NOT re-throw.
        }
    }

    public async Task<IEnumerable<AuditEntry>> QueryAsync(
        int tenantId, DateTime? from, DateTime? to, int limit, int offset)
    {
        // Read path — delegates directly to the repository.
        // No swallowing here: a failed read should return a proper error to the caller.
        return await _repo.QueryAsync(tenantId, from, to, limit, offset);
    }
}
