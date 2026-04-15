// ─────────────────────────────────────────────────────────────────────────────
// HealthResponse.cs
// DTO for the GET /health endpoint. Reports the status of each component
// (MySQL, SignalR, Netbird) and an overall status string.
//
// WHY: The /health endpoint is exempt from API key auth — monitoring tools
// and load balancers poll it to determine if the service is ready. Returning
// component-level status (not just 200/500) allows partial-failure detection:
// e.g., Netbird is down but the core API still works → status = "degraded".
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Domain.DTOs.Responses;

public class HealthResponse
{
    // Overall status: "healthy" | "degraded" | "unhealthy"
    // healthy = all components up
    // degraded = some optional components (e.g., Netbird) are down
    // unhealthy = core components (MySQL or SignalR) are down
    public string Status { get; set; } = string.Empty;

    // Per-component status dictionary, e.g. { "mysql": "healthy", "signalr": "unhealthy" }
    // Dictionary<string, string> serializes to a JSON object: { "mysql": "healthy" }
    public Dictionary<string, string> Components { get; set; } = [];

    // Explicit boolean for dashboard use — faster to check than parsing Components["signalr"]
    public bool SignalrConnected { get; set; }

    // When this health check was run. UTC timestamp for cross-timezone consistency.
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
