// DashboardSummary.cs — Aggregated statistics for the dashboard overview widget.
// Not a database table — built by ControlItFacade from multiple repository queries.
// Each field MUST be a real query result. Never hardcode -1 or placeholder values.

namespace ControlIT.Api.Domain.Models;

/// <summary>
/// Aggregated dashboard statistics. Built by ControlItFacade.GetDashboardSummaryAsync().
/// All counts are real — never hardcoded or estimated.
/// </summary>
public class DashboardSummary
{
    // Total number of devices registered for this tenant
    public int TotalDevices { get; set; }

    // Number of devices that checked in within the last 5 minutes.
    // MUST be a real COUNT query: SELECT COUNT(*) FROM devices WHERE last_access >= DATE_SUB(NOW(), INTERVAL 5 MINUTE)
    // NEVER hardcode -1 or any placeholder value here.
    public int OnlineDevices { get; set; }

    // Number of tenants. Phase 1: always 1 (single-tenant deployment).
    // Phase 2: will be a real count when multi-tenant is introduced.
    public int TotalTenants { get; set; }

    // Total number of events recorded for this tenant
    public int TotalEvents { get; set; }

    // Number of critical security alerts. Phase 1: always 0 (Wazuh not connected).
    // Phase 2: populated from Wazuh via IWazuhClient.
    public int CriticalAlerts { get; set; }

    // When this summary was computed — DateTime.Now (local time, matches MySQL DateTimeKind.Unspecified).
    public DateTime ServerTime { get; set; }
}
