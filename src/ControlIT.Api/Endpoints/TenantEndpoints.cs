namespace ControlIT.Api.Endpoints;

using ControlIT.Api.Application;
using ControlIT.Api.Domain.Interfaces;

public static class TenantEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/tenants", async (ITenantRepository repo, TenantContext tenant) =>
        {
            if (tenant.IsAllTenants)
                return Results.Ok(await repo.GetAllAsync());

            var single = await repo.GetByIdAsync(tenant.TenantId!.Value);
            return single is null
                ? Results.Ok(Array.Empty<object>())
                : Results.Ok(new[] { single });
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");

        app.MapGet("/tenants/{id:int}", async (int id, ITenantRepository repo, TenantContext tenant) =>
        {
            if (!tenant.IsAllTenants && tenant.TenantId != id)
                return Results.Forbid();

            var result = await repo.GetByIdAsync(id);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");

        app.MapGet("/tenants/{id:int}/locations", async (int id, ITenantRepository repo, TenantContext tenant) =>
        {
            if (!tenant.IsAllTenants && tenant.TenantId != id)
                return Results.Forbid();

            var locations = await repo.GetLocationsByTenantAsync(id);
            return Results.Ok(locations);
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");
    }
}
