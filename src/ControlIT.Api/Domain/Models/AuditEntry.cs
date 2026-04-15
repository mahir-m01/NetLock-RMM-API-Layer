// AuditEntry.cs — Domain model for audit log entries written to controlit_audit_log.
// Pattern: Domain Model (EF Core entity)
//
// Unlike the NetLock models (Device, Tenant, etc.) which are READ via Dapper,
// AuditEntry is a ControlIT-OWNED entity managed by EF Core.
// The `controlit_audit_log` table is created and managed by ControlIT's EF migrations.
//
// WHY audit before the operation:
// DPDP Act 2023 compliance requires capturing the ATTEMPT, not just the outcome.
// If a command crashes halfway through, we still have a "PENDING" record proving it was tried.
// The endpoint then writes a second entry with SUCCESS/TIMEOUT/FAILURE after completion.
//
// TenantId ALWAYS comes from TenantContext (set by ApiKeyMiddleware from DB).
// Never accept TenantId from request bodies, headers, or query params.

namespace ControlIT.Api.Domain.Models;

/// <summary>
/// An immutable record of an administrative action. Written to controlit_audit_log.
/// TenantId is always sourced from TenantContext — never from client input.
/// EF Core entity — managed by ControlItDbContext.
/// </summary>
public class AuditEntry
{
    // Auto-increment primary key — EF Core generates this on INSERT
    public long Id { get; set; }

    // When this audit record was created. Defaults to UtcNow on construction.
    // The DB also has UTC_TIMESTAMP() as default, but we set it in code for consistency.
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // The tenant this audit record belongs to. Always from TenantContext — never from request.
    public int TenantId { get; set; }

    // First 16 characters of the SHA-256 hash of the API key that made this request.
    // Sufficient for traceability without exposing the full hash or the raw key.
    public string ActorKeyId { get; set; } = string.Empty;

    // What action was performed. Constants: "COMMAND_EXECUTE", "DEVICE_ENROL_MESH",
    // "NETWORK_PEER_DELETE", "ALERT_ACKNOWLEDGE" (Phase 2)
    public string Action { get; set; } = string.Empty;

    // The type of resource the action was performed on: "Device", "NetworkPeer", "SecurityAlert"
    public string ResourceType { get; set; } = string.Empty;

    // The specific resource ID (device ID, peer ID, alert ID). Nullable — not all actions have one.
    public string? ResourceId { get; set; }

    // Client IP address for traceability. IPv4 or IPv6 (max 45 chars for IPv6).
    public string? IpAddress { get; set; }

    // Outcome: "PENDING" (written before execution), "SUCCESS", "FAILURE", "TIMEOUT"
    public string Result { get; set; } = string.Empty;

    // Error message if Result is FAILURE or TIMEOUT. Null on success.
    public string? ErrorMessage { get; set; }
}
