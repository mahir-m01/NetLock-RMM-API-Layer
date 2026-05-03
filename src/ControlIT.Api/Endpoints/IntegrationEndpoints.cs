// ─────────────────────────────────────────────────────────────────────────────
// IntegrationEndpoints.cs
// Conditionally registers Wazuh (SIEM) endpoints.
//
// WHY conditional Wazuh registration: IWazuhClient has no Phase 1 implementation.
// Registering the routes unconditionally would cause a 500 on every Wazuh request
// (DI would fail to resolve IWazuhClient). The `if (Wazuh:Enabled)` guard means
// those routes simply don't exist unless Wazuh is configured.
//
// Netbird endpoints have moved to NetworkEndpoints.cs (Phase 2D/2E).
//
// Audit coverage: All state-mutating operations write audit entries both
// before (PENDING) and after (SUCCESS) execution.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Endpoints;

using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using ControlIT.Api.Application;
using ControlIT.Api.Domain.DTOs.Requests;

public static class IntegrationEndpoints
{
    public static void Map(WebApplication app)
    {
        // ── Wazuh — Phase 2 (conditional registration) ───────────────────────
        // These routes are only registered when Wazuh:Enabled = true in config.
        // WHY conditional: IWazuhClient has no Phase 1 implementation.
        // If these routes were registered unconditionally, every request would
        // fail with an InvalidOperationException from the DI container.
        if (app.Configuration.GetValue<bool>("Wazuh:Enabled"))
        {
            // GET /alerts/wazuh — list Wazuh security alerts with filtering
            app.MapGet("/alerts/wazuh", async (
                [AsParameters] AlertFilter filter,
                IWazuhClient wazuh) =>
            {
                var alerts = await wazuh.GetAlertsAsync(filter);
                return Results.Ok(alerts);
            }).RequireRateLimiting("api").RequireAuthorization("TenantMember");

            // POST /alerts/acknowledge?alertId=<id> — acknowledge a security alert
            app.MapPost("/alerts/acknowledge", async (
                string alertId,
                IWazuhClient wazuh,
                IAuditService audit,
                TenantContext tenant,
                IActorContext actor) =>
            {
                await audit.RecordAsync(new AuditEntry
                {
                    TenantId = tenant.TenantId ?? 0,
                    ActorKeyId = actor.UserId.ToString(),
                    ActorEmail = actor.Email,
                    Action = "ALERT_ACKNOWLEDGE",
                    ResourceType = "SecurityAlert",
                    ResourceId = alertId,
                    IpAddress = actor.IpAddress,
                    Result = "PENDING"
                });

                await wazuh.AcknowledgeAlertAsync(alertId);

                await audit.RecordAsync(new AuditEntry
                {
                    TenantId = tenant.TenantId ?? 0,
                    ActorKeyId = actor.UserId.ToString(),
                    ActorEmail = actor.Email,
                    Action = "ALERT_ACKNOWLEDGE",
                    ResourceType = "SecurityAlert",
                    ResourceId = alertId,
                    IpAddress = actor.IpAddress,
                    Result = "SUCCESS"
                });

                return Results.Ok();
            }).RequireRateLimiting("api").RequireAuthorization("TenantMember");
        }
    }
}
