// ─────────────────────────────────────────────────────────────────────────────
// IntegrationEndpoints.cs
// Registers Netbird (mesh VPN) endpoints in Phase 1, and conditionally
// registers Wazuh (SIEM) endpoints in Phase 2.
//
// WHY conditional Wazuh registration: IWazuhClient has no Phase 1 implementation.
// Registering the routes unconditionally would cause a 500 on every Wazuh request
// (DI would fail to resolve IWazuhClient). The `if (Wazuh:Enabled)` guard means
// those routes simply don't exist unless Wazuh is configured.
//
// Audit coverage: All state-mutating operations (enrol, delete, acknowledge)
// write audit entries both before (PENDING) and after (SUCCESS) execution.
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
        // ── Netbird — Phase 1 ─────────────────────────────────────────────────

        // GET /network/peers — list all Netbird mesh peers
        app.MapGet("/network/peers", async (INetbirdClient netbird) =>
        {
            var peers = await netbird.GetPeersAsync();
            return Results.Ok(peers);
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");

        // GET /network/peers/{id} — get a specific peer by Netbird peer ID
        app.MapGet("/network/peers/{id}", async (string id, INetbirdClient netbird) =>
        {
            var peer = await netbird.GetPeerByIdAsync(id);
            return peer is null ? Results.NotFound() : Results.Ok(peer);
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");

        // POST /network/enrol?setupKey=<key> — enrol a new peer in the mesh network
        // Writes audit entries before and after for DPDP compliance.
        app.MapPost("/network/enrol", async (
            string setupKey,
            INetbirdClient netbird,
            IAuditService audit,
            TenantContext tenant,
            IActorContext actor) =>
        {
            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenant.TenantId ?? 0,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "DEVICE_ENROL_MESH",
                ResourceType = "NetworkPeer",
                IpAddress = actor.IpAddress,
                Result = "PENDING"
            });

            await netbird.EnrolPeerAsync(setupKey);

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenant.TenantId ?? 0,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "DEVICE_ENROL_MESH",
                ResourceType = "NetworkPeer",
                IpAddress = actor.IpAddress,
                Result = "SUCCESS"
            });

            return Results.Ok();
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");

        // DELETE /network/peer/{id} — remove a peer from the mesh network
        app.MapDelete("/network/peer/{id}", async (
            string id,
            INetbirdClient netbird,
            IAuditService audit,
            TenantContext tenant,
            IActorContext actor) =>
        {
            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenant.TenantId ?? 0,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "NETWORK_PEER_DELETE",
                ResourceType = "NetworkPeer",
                ResourceId = id,
                IpAddress = actor.IpAddress,
                Result = "PENDING"
            });

            await netbird.RemovePeerAsync(id);

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenant.TenantId ?? 0,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "NETWORK_PEER_DELETE",
                ResourceType = "NetworkPeer",
                ResourceId = id,
                IpAddress = actor.IpAddress,
                Result = "SUCCESS"
            });

            return Results.NoContent();
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");

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
