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
using Microsoft.Extensions.Caching.Memory;

public static class HealthEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/health", async (
            IDbConnectionFactory dbFactory,
            IEndpointProvider endpoint,
            INetbirdClient netbird,
            IMemoryCache cache) =>
        {
            var cached = await cache.GetOrCreateAsync("health:status", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);

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

                return new { Response = response, IsUnhealthy = status == "unhealthy" };
            });

            // Return 503 if fully unhealthy — load balancers interpret this as "take offline".
            // Return 200 for healthy or degraded — the service is still accepting requests.
            return cached!.IsUnhealthy
                ? Results.Json(cached.Response, statusCode: 503)
                : Results.Ok(cached.Response);
        });

        app.MapGet("/healthz", () => Results.Ok(new { status = "alive" })).AllowAnonymous();

        app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();

        app.MapGet("/health/ready", async (
            IDbConnectionFactory dbFactory,
            IEndpointProvider endpoint) =>
        {
            var components = new Dictionary<string, string>();
            var healthy = true;

            try
            {
                using var conn = await dbFactory.CreateConnectionAsync();
                components["mysql"] = "ok";
            }
            catch
            {
                components["mysql"] = "error";
                healthy = false;
            }

            components["signalr"] = endpoint.IsConnected ? "ok" : "error";
            if (!endpoint.IsConnected) healthy = false;

            var response = new { status = healthy ? "healthy" : "unhealthy", components };
            return healthy ? Results.Ok(response) : Results.Json(response, statusCode: 503);
        }).AllowAnonymous();
    }
}
