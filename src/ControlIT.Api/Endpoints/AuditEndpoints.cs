// ─────────────────────────────────────────────────────────────────────────────
// AuditEndpoints.cs
// Registers GET /audit/logs — query the audit trail for the authenticated tenant.
//
// WHY scoped to caller's tenant: Each tenant can only query their own audit log.
// tenant.TenantId comes from TenantContext (set by ApiKeyMiddleware, not the request).
// This ensures cross-tenant data leakage is impossible at the HTTP layer.
//
// Compliance note: Audit logs support DPDP Act 2023 compliance — every state-
// mutating action (commands, network enrol/delete) is recorded here.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Endpoints;

using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Application;

public static class AuditEndpoints
{
    public static void Map(WebApplication app)
    {
        // GET /audit/logs?from=2024-01-01&to=2024-01-31&limit=50&offset=0
        // All parameters are optional — omit for a full (limited) audit history.
        app.MapGet("/audit/logs", async (
            DateTime? from,
            DateTime? to,
            int limit,
            int offset,
            IAuditService audit,
            TenantContext tenant) =>
        {
            // Default limit = 50, max = 500, min = 1.
            // limit == 0 means "no limit was provided" — default to 50.
            limit = Math.Clamp(limit == 0 ? 50 : limit, 1, 500);

            // tenant.TenantId is from TenantContext — set by ApiKeyMiddleware from DB.
            // Never read tenant_id from a query parameter for audit queries.
            var entries = await audit.QueryAsync(tenant.TenantId, from, to, limit, offset);
            return Results.Ok(entries);
        }).RequireRateLimiting("api");
    }
}
