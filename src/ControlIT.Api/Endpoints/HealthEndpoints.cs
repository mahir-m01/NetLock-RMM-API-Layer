// ─────────────────────────────────────────────────────────────────────────────
// HealthEndpoints.cs
// Registers the GET /health endpoint.
//
// WHY no auth: The /health path is explicitly exempted in ApiKeyMiddleware.
// Monitoring tools (uptime checkers, load balancers, Kubernetes probes) must
// be able to check health without an API key. This is standard practice.
//
// Status logic:
//   healthy  = MySQL + SignalR both up
//   degraded = Netbird down (optional integration), core is fine
//   unhealthy = MySQL or SignalR down (core functionality broken)
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Endpoints;

using ControlIT.Api.Domain.DTOs.Responses;
using ControlIT.Api.Domain.Interfaces;

public static class HealthEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/health", async (
            IDbConnectionFactory dbFactory,
            IEndpointProvider endpoint,
            INetbirdClient netbird) =>
        {
            var components = new Dictionary<string, string>();
            var allHealthy = true;

            // ── MySQL health check ────────────────────────────────────────────
            // Try to create a connection — if it throws, MySQL is unreachable.
            try
            {
                using var conn = await dbFactory.CreateConnectionAsync();
                components["mysql"] = "healthy";
            }
            catch
            {
                components["mysql"] = "unhealthy";
                allHealthy = false;
            }

            // ── SignalR health check ──────────────────────────────────────────
            // Reads HubConnection.State directly — no DB call needed.
            components["signalr"] = endpoint.IsConnected ? "healthy" : "unhealthy";
            if (!endpoint.IsConnected) allHealthy = false;

            // ── Netbird health check ──────────────────────────────────────────
            // GetPeersAsync makes a real HTTP call to the Netbird management server.
            // If Netbird is down, status = "degraded" (not "unhealthy") because
            // the core API still functions without Netbird.
            try
            {
                await netbird.GetPeersAsync();
                components["netbird"] = "healthy";
            }
            catch
            {
                components["netbird"] = "unhealthy";
                // Netbird being down = degraded, not fully unhealthy — don't set allHealthy = false
            }

            // Determine overall status from component statuses.
            var status = allHealthy ? "healthy"
                : components.Values.All(v => v == "unhealthy") ? "unhealthy"
                : "degraded";

            var response = new HealthResponse
            {
                Status = status,
                Components = components,
                SignalrConnected = endpoint.IsConnected
            };

            // Return 503 if fully unhealthy — load balancers interpret this as "take offline".
            // Return 200 for healthy or degraded — the service is still accepting requests.
            return status == "unhealthy"
                ? Results.Json(response, statusCode: 503)
                : Results.Ok(response);
        });
        // /health is intentionally exempt from rate limiting so monitoring tools can poll freely.
    }
}
