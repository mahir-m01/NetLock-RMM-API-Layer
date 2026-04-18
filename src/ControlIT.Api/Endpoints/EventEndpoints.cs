// ─────────────────────────────────────────────────────────────────────────────
// EventEndpoints.cs
// Registers the GET /events endpoint with pagination.
//
// Events are filtered to the authenticated tenant's devices.
// The events table joins to tenants by name (not ID) — this is handled
// transparently by MySqlEventRepository.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Endpoints;

using ControlIT.Api.Application;

public static class EventEndpoints
{
    public static void Map(WebApplication app)
    {
        // GET /events?page=1&pageSize=25
        // page and pageSize are bound from query string parameters directly.
        // Validated and clamped here (not in the facade) because this is HTTP input validation.
        app.MapGet("/events", async (
            int page,
            int pageSize,
            ControlItFacade facade,
            TenantContext tenant) =>
        {
            // Clamp to valid ranges:
            // page: min 1 (no page 0)
            // pageSize: 1 to 100 (prevent requesting 10,000 events at once)
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var result = await facade.GetEventsAsync(tenant, page, pageSize);
            return Results.Ok(result);
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");
    }
}
