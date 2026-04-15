// SecurityAlert.cs — Domain model for security alerts from external SIEM systems (Phase 2).
// Phase 2: populated from Wazuh's alerts API via WazuhApiClient.
// Phase 1: the interface and model exist but no Wazuh endpoints are registered unless
// Wazuh:Enabled = true in configuration.

namespace ControlIT.Api.Domain.Models;

/// <summary>
/// Represents a security alert from an external SIEM (e.g., Wazuh).
/// Phase 2 model — not used in Phase 1.
/// </summary>
public class SecurityAlert
{
    // Alert ID as returned by the SIEM — string because IDs may be non-numeric (UUIDs, etc.)
    public string Id { get; set; } = string.Empty;

    // Which SIEM produced this alert: "Wazuh" in Phase 2
    public string Source { get; set; } = string.Empty;

    // The Wazuh agent (device) name that generated this alert
    public string AgentName { get; set; } = string.Empty;

    // Human-readable description of the triggered rule
    public string RuleDescription { get; set; } = string.Empty;

    // Wazuh rule severity level (1-15). Higher = more critical.
    // Typically: 1-7 = low, 8-11 = medium, 12-15 = high/critical
    public int RuleLevel { get; set; }

    // When the alert was triggered on the SIEM
    public DateTime Timestamp { get; set; }

    // Whether an admin has acknowledged (dismissed/actioned) this alert
    public bool Acknowledged { get; set; }
}
