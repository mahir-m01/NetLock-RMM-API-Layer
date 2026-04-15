// ─────────────────────────────────────────────────────────────────────────────
// IWazuhClient.cs
// Phase 2 stub — the interface is defined now so Phase 1 code can reference it
// (e.g., in IntegrationEndpoints for conditional Wazuh registration) without
// a concrete implementation existing yet.
//
// WHY: Defining the interface in Phase 1 means no breaking changes to the
// application layer when Phase 2 arrives. WazuhApiClient just needs to implement
// this interface and be registered in DI.
//
// Implementation note: HTTP-only. No Wazuh libraries compiled in.
// Wazuh endpoints are registered conditionally (Wazuh:Enabled = true in config).
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Domain.Interfaces;

using ControlIT.Api.Domain.Models;
using ControlIT.Api.Domain.DTOs.Requests;

/// <summary>
/// Phase 2 stub. Implementation: HTTP-only WazuhApiClient. No Wazuh libraries compiled in.
/// </summary>
public interface IWazuhClient
{
    // Returns security alerts filtered by level, acknowledgement status, and date range.
    Task<IEnumerable<SecurityAlert>> GetAlertsAsync(AlertFilter filter,
        CancellationToken cancellationToken = default);

    // Marks an alert as acknowledged — suppresses it from the dashboard alert count.
    Task AcknowledgeAlertAsync(string alertId, CancellationToken cancellationToken = default);
}
