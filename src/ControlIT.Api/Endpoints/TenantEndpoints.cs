// ─────────────────────────────────────────────────────────────────────────────
// TenantEndpoints.cs
// Registers tenant and location management endpoints.
//
// Note: These endpoints are admin-level — they list ALL tenants, not just
// the tenant associated with the API key. In Phase 1 this is acceptable
// because we have a single tenant. Phase 2 will add role-based auth to
// restrict tenant listing to users with an "admin" role.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Endpoints;

using ControlIT.Api.Domain.Interfaces;

public static class TenantEndpoints
{
    public static void Map(WebApplication app)
    {
        // GET /tenants — list all tenants.
        // Admin only — Phase 1 does not enforce role distinctions.
        // Phase 2: add role check when JWT auth is introduced.
        app.MapGet("/tenants", async (ITenantRepository repo) =>
        {
            var tenants = await repo.GetAllAsync();
            return Results.Ok(tenants);
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");

        // GET /tenants/{id} — get a specific tenant with its locations.
        // Uses QueryMultipleAsync internally for one DB round-trip.
        app.MapGet("/tenants/{id:int}", async (int id, ITenantRepository repo) =>
        {
            var tenant = await repo.GetByIdAsync(id);
            return tenant is null ? Results.NotFound() : Results.Ok(tenant);
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");

        // GET /tenants/{id}/locations — locations for a specific tenant.
        // Separate endpoint so the dashboard can load locations without the full tenant.
        app.MapGet("/tenants/{id:int}/locations", async (
            int id, ITenantRepository repo) =>
        {
            var locations = await repo.GetLocationsByTenantAsync(id);
            return Results.Ok(locations);
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");
    }
}
