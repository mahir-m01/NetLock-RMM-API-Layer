// ─────────────────────────────────────────────────────────────────────────────
// DashboardEndpoints.cs
// Registers GET /dashboard — returns the DashboardSummary.
//
// The dashboard summary includes:
//   - TotalDevices: COUNT from the devices table (real query)
//   - OnlineDevices: COUNT of devices with last_access within 5 minutes (real query)
//   - TotalTenants: 1 (Phase 1: single-tenant)
//   - TotalEvents: COUNT from the events table (real query)
//   - CriticalAlerts: 0 (Phase 2: Wazuh)
//
// CRITICAL: OnlineDevices MUST come from a real COUNT query — never hardcode -1
// or any placeholder value. The dashboard uses this to show device health.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Endpoints;

using ControlIT.Api.Application;

public static class DashboardEndpoints
{
    public static void Map(WebApplication app)
    {
        // GET /dashboard
        // Returns the full DashboardSummary for the authenticated tenant.
        // The facade makes 3 queries: devices count, online count, events count.
        app.MapGet("/dashboard", async (
            ControlItFacade facade,
            TenantContext tenant) =>
        {
            var summary = await facade.GetDashboardSummaryAsync(tenant);
            return Results.Ok(summary);
        }).RequireRateLimiting("api");
    }
}
