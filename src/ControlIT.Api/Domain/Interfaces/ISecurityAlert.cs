// ─────────────────────────────────────────────────────────────────────────────
// ISecurityAlert.cs
// Pattern: Adapter (Phase 2) — a higher-level abstraction over security alert
// sources. Phase 2 will implement WazuhAlertAdapter using this interface,
// which in turn uses IWazuhClient to fetch and normalize Wazuh alerts.
//
// WHY: Separating the "get active alerts" concern from the Wazuh-specific HTTP
// client means the dashboard can show alerts from multiple sources in Phase 3
// (Wazuh + other SIEM tools) without changing the endpoint code.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Domain.Interfaces;

using ControlIT.Api.Domain.Models;

/// <summary>
/// Adapter interface for security alert sources. Phase 2.
/// Implemented by WazuhAlertAdapter using the Adapter pattern.
/// </summary>
public interface ISecurityAlert
{
    // Returns all currently active (unacknowledged) alerts for the tenant.
    // Used by the dashboard critical alert count and the alerts list endpoint.
    Task<IEnumerable<SecurityAlert>> GetActiveAlertsAsync(
        CancellationToken cancellationToken = default);
}
