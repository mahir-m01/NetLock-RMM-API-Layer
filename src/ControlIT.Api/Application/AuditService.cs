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

using ControlIT.Api.Domain.DTOs.Responses;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using ControlIT.Api.Infrastructure.Persistence;

public class AuditService : IAuditService
{
    private readonly AuditRepository _repo;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AuditRepository repo, ILogger<AuditService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // Implements the RecordAsync contract: must NEVER throw. All exceptions
    // are caught, logged at Critical, and swallowed so the caller continues.
    public async Task RecordAsync(AuditEntry entry)
    {
        try
        {
            await _repo.InsertAsync(entry);
        }
        catch (Exception ex)
        {
            // LogCritical so the event triggers on-call alerts.
            // Action and TenantId are logged for triage. Sensitive fields (command, API key)
            // are never included in log output.
            _logger.LogCritical(ex,
                "AUDIT WRITE FAILED for Action={Action} TenantId={TenantId}. " +
                "The operation will continue but this event has no audit trail.",
                entry.Action, entry.TenantId);
            // Exception is intentionally swallowed — do NOT re-throw.
        }
    }

    public async Task<IEnumerable<AuditLogResponse>> QueryAsync(
        int? tenantId, DateTime? from, DateTime? to, int limit, int offset)
    {
        var entries = await _repo.QueryAsync(tenantId, from, to, limit, offset);
        return entries.Select(e => new AuditLogResponse
        {
            Id = e.Id,
            Timestamp = e.Timestamp,
            TenantId = e.TenantId,
            ActorEmail = e.ActorEmail ?? e.ActorKeyId, // fallback for legacy API-key entries
            Action = e.Action,
            ResourceType = e.ResourceType,
            ResourceId = e.ResourceId,
            IpAddress = e.IpAddress,
            Result = e.Result,
            ErrorMessage = e.ErrorMessage
        });
    }
}
