// ─────────────────────────────────────────────────────────────────────────────
// DeviceEndpoints.cs
// Registers device-related endpoints: list, get by ID, metrics.
//
// Pattern used in all endpoints:
//   - Parameters are injected from the DI container (ControlItFacade, TenantContext)
//   - [AsParameters] binds query string parameters to a strongly-typed object
//   - TenantContext is injected (not read from request) — it was set by ApiKeyMiddleware
//   - All routes use RequireRateLimiting("api") — 120 requests/minute limit
//
// WHY use ControlItFacade (not repositories directly):
// Endpoints handle HTTP concerns only. All business logic lives in the facade.
// This separation makes the facade unit-testable without an HTTP stack.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Endpoints;

using ControlIT.Api.Application;
using ControlIT.Api.Domain.DTOs.Requests;

public static class DeviceEndpoints
{
    public static void Map(WebApplication app)
    {
        // GET /devices?page=1&pageSize=25&platform=Windows&onlineOnly=true&searchTerm=server
        // [AsParameters] binds all DeviceFilter properties from query string parameters.
        // This is Minimal API's equivalent of [FromQuery] on a controller action parameter.
        app.MapGet("/devices", async (
            [AsParameters] DeviceFilter filter,
            ControlItFacade facade,
            TenantContext tenant) =>
        {
            var result = await facade.GetDevicesAsync(filter, tenant);
            return Results.Ok(result);
        }).RequireRateLimiting("api");

        // GET /devices/{id}
        // {id:int} constrains the route parameter to integers only.
        // Non-integer values will return 400 automatically.
        app.MapGet("/devices/{id:int}", async (
            int id,
            ControlItFacade facade,
            TenantContext tenant) =>
        {
            var device = await facade.GetDeviceByIdAsync(id, tenant);
            // Results.NotFound() returns 404 with no body.
            // Results.Ok(device) returns 200 with the device as JSON.
            return device is null ? Results.NotFound() : Results.Ok(device);
        }).RequireRateLimiting("api");

        // GET /devices/metrics — returns just TotalDevices and OnlineDevices
        // Used by the dashboard for the device count widget without a full device list.
        app.MapGet("/devices/metrics", async (
            ControlItFacade facade,
            TenantContext tenant) =>
        {
            var summary = await facade.GetDashboardSummaryAsync(tenant);
            // Return only the device-specific fields, not the full dashboard summary.
            return Results.Ok(new
            {
                summary.TotalDevices,
                summary.OnlineDevices
            });
        }).RequireRateLimiting("api");
    }
}
