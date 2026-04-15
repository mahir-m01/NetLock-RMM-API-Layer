// AlertFilter.cs — Query parameters for GET /alerts/wazuh (Phase 2).
// Exists in Phase 1 to define the interface contract for the Wazuh integration.
// The endpoint itself is only registered when Wazuh:Enabled = true.

namespace ControlIT.Api.Domain.DTOs.Requests;

/// <summary>
/// Query parameters for the Phase 2 GET /alerts/wazuh endpoint.
/// Bound via [AsParameters] in the endpoint handler.
/// </summary>
public class AlertFilter
{
    // Minimum Wazuh rule level to include (1-15). Null = include all levels.
    public int? MinLevel { get; set; }

    // If true, only return alerts that haven't been acknowledged yet.
    public bool? UnacknowledgedOnly { get; set; }

    // Start of the time range filter. Null = no lower bound.
    public DateTime? From { get; set; }

    // End of the time range filter. Null = no upper bound.
    public DateTime? To { get; set; }

    // Pagination — same pattern as DeviceFilter
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
