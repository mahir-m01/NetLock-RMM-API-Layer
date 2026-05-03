// ─────────────────────────────────────────────────────────────────────────────
// NetworkEndpoints.cs
// Registers all Netbird mesh VPN endpoints (Phase 2D/2E). Replaces the Netbird
// section that previously lived in IntegrationEndpoints.cs.
//
// Tenant isolation: Every mutating endpoint verifies that the target resource
// (setup key, peer, mapping) belongs to the caller's tenant group before
// proceeding. Read endpoints return only tenant-scoped data.
//
// Audit coverage: All state-mutating operations write PENDING before and
// SUCCESS after execution for DPDP compliance.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Endpoints;

using ControlIT.Api.Application;
using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.DTOs.Responses;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.Extensions.Caching.Memory;

file static class TenantResolutionExtensions
{
    /// <summary>
    /// Converts a failed TenantResolutionResult into the correct IResult (400 or 403).
    /// Call only when result.IsSuccess == false.
    /// </summary>
    internal static IResult ToErrorResult(this TenantResolutionResult result) =>
        result.StatusCode == 403
            ? Results.Forbid()
            : Results.BadRequest(new { error = result.Error });
}

public static class NetworkEndpoints
{
    public static void Map(WebApplication app)
    {
        // ── Peers (read) ────────────────────────────────────────────────────

        // GET /network/groups -- list NetBird groups for elevated tenant binding flows
        app.MapGet("/network/groups", async (
            INetbirdClient netbird,
            IMemoryCache cache) =>
        {
            var groups = await cache.GetOrCreateAsync("netbird:groups", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
                return await netbird.GetGroupsAsync();
            });

            return Results.Ok(groups);
        }).RequireRateLimiting("api").RequireAuthorization("CpAdminOrAbove");

        // GET /network/peers -- list peers scoped to the caller's tenant group
        app.MapGet("/network/peers", async (
            TenantNetworkService networkService,
            TenantContext tenant,
            ITenantRepository tenants,
            int? targetTenantId = null) =>
        {
            var resolution = await TenantTargetResolver.ResolveAsync(tenant, targetTenantId, tenants);
            if (!resolution.IsSuccess) return resolution.ToErrorResult();
            var tenantId = resolution.TenantId!.Value;
            var peers = await networkService.GetTenantPeersAsync(tenantId);
            return Results.Ok(peers);
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");

        // POST /network/tenant-group -- bind an existing NetBird group to a tenant
        app.MapPost("/network/tenant-group", async (
            BindTenantGroupRequest request,
            TenantNetworkService networkService,
            TenantContext tenant,
            ITenantRepository tenants,
            IAuditService audit,
            IActorContext actor,
            int? targetTenantId = null) =>
        {
            var resolution = await TenantTargetResolver.ResolveAsync(tenant, targetTenantId, tenants);
            if (!resolution.IsSuccess) return resolution.ToErrorResult();
            var tenantId = resolution.TenantId!.Value;

            var groupId = request.GroupId.Trim();
            var mode = request.Mode.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(groupId))
                return Results.BadRequest(new { error = "groupId is required." });

            if (mode is not TenantNetbirdGroupMode.External and not TenantNetbirdGroupMode.ReadOnly)
                return Results.BadRequest(new { error = "mode must be external or read_only." });

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenantId,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "TENANT_NETBIRD_GROUP_BIND",
                ResourceType = "TenantNetbirdGroup",
                ResourceId = groupId,
                IpAddress = actor.IpAddress,
                Result = "PENDING"
            });

            TenantNetbirdGroup mapping;
            try
            {
                mapping = await networkService.BindTenantGroupAsync(tenantId, groupId, mode);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = "NetBird group not found." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenantId,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "TENANT_NETBIRD_GROUP_BIND",
                ResourceType = "TenantNetbirdGroup",
                ResourceId = groupId,
                IpAddress = actor.IpAddress,
                Result = "SUCCESS"
            });

            return Results.Ok(mapping);
        }).RequireRateLimiting("api").RequireAuthorization("CpAdminOrAbove");

        // GET /network/peers/{id} -- get a specific peer by Netbird peer ID
        app.MapGet("/network/peers/{id}", async (
            string id,
            INetbirdClient netbird,
            TenantNetworkService networkService,
            TenantContext tenant,
            ITenantRepository tenants,
            int? targetTenantId = null) =>
        {
            var resolution = await TenantTargetResolver.ResolveAsync(tenant, targetTenantId, tenants);
            if (!resolution.IsSuccess) return resolution.ToErrorResult();
            var tenantId = resolution.TenantId!.Value;

            var peer = await netbird.GetPeerByIdAsync(id);
            if (peer is null)
                return Results.NotFound();

            var tenantGroup = await networkService.GetTenantGroupAsync(tenantId);
            if (tenantGroup is null || !peer.Groups.Any(g => g.Id == tenantGroup.NetbirdGroupId))
                return Results.Forbid();

            return Results.Ok(peer);
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");

        // ── Peers (mutate) ──────────────────────────────────────────────────

        // DELETE /network/peer/{id} -- remove peer from mesh and clean up mapping
        app.MapDelete("/network/peer/{id}", async (
            string id,
            INetbirdClient netbird,
            INetbirdMappingRepository mappingRepo,
            IDeviceRepository devices,
            TenantNetworkService networkService,
            IAuditService audit,
            TenantContext tenant,
            IActorContext actor,
            ITenantRepository tenants,
            int? targetTenantId = null) =>
        {
            var resolution = await TenantTargetResolver.ResolveAsync(tenant, targetTenantId, tenants);
            if (!resolution.IsSuccess) return resolution.ToErrorResult();
            var tenantId = resolution.TenantId!.Value;

            var peer = await netbird.GetPeerByIdAsync(id);
            if (peer is null)
                return Results.NotFound();

            var tenantGroup = await networkService.GetTenantGroupAsync(tenantId);
            if (tenantGroup is null || !peer.Groups.Any(g => g.Id == tenantGroup.NetbirdGroupId))
                return Results.Forbid();
            if (tenantGroup.GroupMode == TenantNetbirdGroupMode.ReadOnly)
                return Results.BadRequest(new { error = "Tenant NetBird group is read-only." });

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenantId,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "NETWORK_PEER_DELETE",
                ResourceType = "NetworkPeer",
                ResourceId = id,
                IpAddress = actor.IpAddress,
                Result = "PENDING"
            });

            await netbird.RemovePeerAsync(id);
            await mappingRepo.DeleteByPeerIdAsync(id);

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenantId,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "NETWORK_PEER_DELETE",
                ResourceType = "NetworkPeer",
                ResourceId = id,
                IpAddress = actor.IpAddress,
                Result = "SUCCESS"
            });

            return Results.NoContent();
        }).RequireRateLimiting("api").RequireAuthorization("CpAdminOrAbove");

        // ── Setup Keys ──────────────────────────────────────────────────────

        // GET /network/setup-keys -- list setup keys scoped to tenant's group.
        // The raw Key is always redacted regardless of caller role — setup keys are
        // reusable enrollment secrets and must never be exposed after creation.
        app.MapGet("/network/setup-keys", async (
            INetbirdClient netbird,
            TenantNetworkService networkService,
            TenantContext tenant,
            ITenantRepository tenants,
            int? targetTenantId = null) =>
        {
            var resolution = await TenantTargetResolver.ResolveAsync(tenant, targetTenantId, tenants);
            if (!resolution.IsSuccess) return resolution.ToErrorResult();
            var tenantId = resolution.TenantId!.Value;
            var tenantGroup = await networkService.GetTenantGroupAsync(tenantId);
            if (tenantGroup is null)
                return Results.Ok(Array.Empty<SetupKeyListResponse>());

            var allKeys = await netbird.GetSetupKeysAsync();
            var result = allKeys
                .Where(k => k.AutoGroups.Contains(tenantGroup.NetbirdGroupId))
                .Select(k => new SetupKeyListResponse(
                    Id: k.Id,
                    Name: k.Name,
                    Key: "[redacted]",
                    Type: k.Type,
                    Valid: k.Valid,
                    Revoked: k.Revoked,
                    UsedTimes: k.UsedTimes,
                    UsageLimit: k.UsageLimit,
                    Expires: k.Expires,
                    AutoGroups: k.AutoGroups,
                    Ephemeral: k.Ephemeral,
                    State: k.State));

            return Results.Ok(result);
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");

        // POST /network/setup-keys -- create a setup key for the tenant
        app.MapPost("/network/setup-keys", async (
            CreateSetupKeyApiRequest request,
            INetbirdClient netbird,
            TenantNetworkService networkService,
            IAuditService audit,
            TenantContext tenant,
            IActorContext actor,
            ITenantRepository tenants,
            int? targetTenantId = null) =>
        {
            var resolution = await TenantTargetResolver.ResolveAsync(tenant, targetTenantId, tenants);
            if (!resolution.IsSuccess) return resolution.ToErrorResult();
            var tenantId = resolution.TenantId!.Value;
            var tenantGroup = await networkService.GetTenantGroupAsync(tenantId);
            if (tenantGroup?.GroupMode == TenantNetbirdGroupMode.ReadOnly)
                return Results.BadRequest(new { error = "Tenant NetBird group is read-only." });

            var tenantGroupId = await networkService.EnsureTenantGroupAsync(tenantId);

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenantId,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "SETUP_KEY_CREATE",
                ResourceType = "SetupKey",
                IpAddress = actor.IpAddress,
                Result = "PENDING"
            });

            var created = await netbird.CreateSetupKeyAsync(new CreateSetupKeyRequest(
                Name: request.Name,
                Type: request.Type,
                ExpiresInSeconds: request.ExpiresInDays * 86400,
                AutoGroups: [tenantGroupId],
                UsageLimit: request.UsageLimit,
                Ephemeral: request.Ephemeral));

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenantId,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "SETUP_KEY_CREATE",
                ResourceType = "SetupKey",
                ResourceId = created.Id,
                IpAddress = actor.IpAddress,
                Result = "SUCCESS"
            });

            // Raw key is returned exactly once here — the creation response.
            // Subsequent list calls via GET /network/setup-keys always redact it.
            return Results.Ok(new SetupKeyCreateResponse(
                Id: created.Id,
                Name: created.Name,
                Key: created.Key,
                Type: created.Type,
                Valid: created.Valid,
                Revoked: created.Revoked,
                UsedTimes: created.UsedTimes,
                UsageLimit: created.UsageLimit,
                Expires: created.Expires,
                AutoGroups: created.AutoGroups,
                Ephemeral: created.Ephemeral,
                State: created.State));
        }).RequireRateLimiting("api").RequireAuthorization("CpAdminOrAbove");

        // DELETE /network/setup-keys/{id} -- delete a setup key (tenant-scoped)
        app.MapDelete("/network/setup-keys/{id}", async (
            string id,
            INetbirdClient netbird,
            TenantNetworkService networkService,
            IAuditService audit,
            TenantContext tenant,
            IActorContext actor,
            ITenantRepository tenants,
            int? targetTenantId = null) =>
        {
            var resolution = await TenantTargetResolver.ResolveAsync(tenant, targetTenantId, tenants);
            if (!resolution.IsSuccess) return resolution.ToErrorResult();
            var tenantId = resolution.TenantId!.Value;
            var key = await netbird.GetSetupKeyByIdAsync(id);
            if (key is null)
                return Results.NotFound();

            var tenantGroup = await networkService.GetTenantGroupAsync(tenantId);
            if (tenantGroup is null || !key.AutoGroups.Contains(tenantGroup.NetbirdGroupId))
                return Results.Forbid();
            if (tenantGroup.GroupMode == TenantNetbirdGroupMode.ReadOnly)
                return Results.BadRequest(new { error = "Tenant NetBird group is read-only." });

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenantId,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "SETUP_KEY_DELETE",
                ResourceType = "SetupKey",
                ResourceId = id,
                IpAddress = actor.IpAddress,
                Result = "PENDING"
            });

            await netbird.DeleteSetupKeyAsync(id);

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenantId,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "SETUP_KEY_DELETE",
                ResourceType = "SetupKey",
                ResourceId = id,
                IpAddress = actor.IpAddress,
                Result = "SUCCESS"
            });

            return Results.NoContent();
        }).RequireRateLimiting("api").RequireAuthorization("CpAdminOrAbove");

        // ── Enrollment ──────────────────────────────────────────────────────

        // POST /network/enrol -- enrol a peer using a setup key (body-based)
        app.MapPost("/network/enrol", async (
            EnrolPeerRequest request,
            INetbirdClient netbird,
            TenantNetworkService networkService,
            IAuditService audit,
            TenantContext tenant,
            IActorContext actor,
            ITenantRepository tenants,
            int? targetTenantId = null) =>
        {
            var resolution = await TenantTargetResolver.ResolveAsync(tenant, targetTenantId, tenants);
            if (!resolution.IsSuccess) return resolution.ToErrorResult();
            var tenantId = resolution.TenantId!.Value;

            // Validate setup key belongs to tenant
            var tenantGroup = await networkService.GetTenantGroupAsync(tenantId);
            if (tenantGroup is null)
                return Results.BadRequest(new { error = "Tenant has no network group configured." });
            if (tenantGroup.GroupMode == TenantNetbirdGroupMode.ReadOnly)
                return Results.BadRequest(new { error = "Tenant NetBird group is read-only." });

            var allKeys = await netbird.GetSetupKeysAsync();
            var matchingKey = allKeys.FirstOrDefault(k =>
                k.Key == request.SetupKey &&
                k.AutoGroups.Contains(tenantGroup.NetbirdGroupId));

            if (matchingKey is null)
                return Results.Forbid();

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenantId,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "DEVICE_ENROL_MESH",
                ResourceType = "NetworkPeer",
                IpAddress = actor.IpAddress,
                Result = "PENDING"
            });

            await netbird.EnrolPeerAsync(request.SetupKey);

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenantId,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "DEVICE_ENROL_MESH",
                ResourceType = "NetworkPeer",
                IpAddress = actor.IpAddress,
                Result = "SUCCESS"
            });

            return Results.Ok();
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");

        // ── Peer-Device Linking ─────────────────────────────────────────────

        // POST /network/peers/{peerId}/link -- link a Netbird peer to a device
        app.MapPost("/network/peers/{peerId}/link", async (
            string peerId,
            LinkPeerRequest request,
            INetbirdClient netbird,
            INetbirdMappingRepository mappingRepo,
            IDeviceRepository devices,
            TenantNetworkService networkService,
            IAuditService audit,
            TenantContext tenant,
            IActorContext actor,
            ITenantRepository tenants,
            int? targetTenantId = null) =>
        {
            var resolution = await TenantTargetResolver.ResolveAsync(tenant, targetTenantId, tenants);
            if (!resolution.IsSuccess) return resolution.ToErrorResult();
            var tenantId = resolution.TenantId!.Value;

            return await PeerDeviceLinkHandler.LinkAsync(
                peerId,
                request.DeviceId,
                netbird,
                mappingRepo,
                devices,
                networkService,
                audit,
                actor,
                tenantId);
        }).RequireRateLimiting("api").RequireAuthorization("CpAdminOrAbove");

        // DELETE /network/peers/{peerId}/link -- unlink a peer from a device
        app.MapDelete("/network/peers/{peerId}/link", async (
            string peerId,
            INetbirdClient netbird,
            INetbirdMappingRepository mappingRepo,
            TenantNetworkService networkService,
            IAuditService audit,
            TenantContext tenant,
            IActorContext actor,
            ITenantRepository tenants,
            int? targetTenantId = null) =>
        {
            var resolution = await TenantTargetResolver.ResolveAsync(tenant, targetTenantId, tenants);
            if (!resolution.IsSuccess) return resolution.ToErrorResult();
            var tenantId = resolution.TenantId!.Value;

            var existing = await mappingRepo.GetByPeerIdAsync(peerId);
            if (existing is null)
                return Results.NotFound();

            var peer = await netbird.GetPeerByIdAsync(peerId);
            var tenantGroup = await networkService.GetTenantGroupAsync(tenantId);
            if (tenantGroup is null || peer is null || !peer.Groups.Any(g => g.Id == tenantGroup.NetbirdGroupId))
                return Results.Forbid();

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenantId,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "DEVICE_PEER_UNLINK",
                ResourceType = "DeviceNetbirdMap",
                ResourceId = peerId,
                IpAddress = actor.IpAddress,
                Result = "PENDING"
            });

            await mappingRepo.DeleteByPeerIdAsync(peerId);

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenantId,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "DEVICE_PEER_UNLINK",
                ResourceType = "DeviceNetbirdMap",
                ResourceId = peerId,
                IpAddress = actor.IpAddress,
                Result = "SUCCESS"
            });

            return Results.NoContent();
        }).RequireRateLimiting("api").RequireAuthorization("CpAdminOrAbove");

        // ── F2: Peer Update ─────────────────────────────────────────────────

        // PUT /network/peers/{id} -- update peer settings (tenant-scoped)
        app.MapPut("/network/peers/{id}", async (
            string id,
            UpdatePeerRequest request,
            INetbirdClient netbird,
            TenantNetworkService networkService,
            IAuditService audit,
            TenantContext tenant,
            IActorContext actor,
            ITenantRepository tenants,
            int? targetTenantId = null) =>
        {
            var resolution = await TenantTargetResolver.ResolveAsync(tenant, targetTenantId, tenants);
            if (!resolution.IsSuccess) return resolution.ToErrorResult();
            var tenantId = resolution.TenantId!.Value;

            var peer = await netbird.GetPeerByIdAsync(id);
            if (peer is null)
                return Results.NotFound();

            var tenantGroup = await networkService.GetTenantGroupAsync(tenantId);
            if (tenantGroup is null || !peer.Groups.Any(g => g.Id == tenantGroup.NetbirdGroupId))
                return Results.Forbid();
            if (tenantGroup.GroupMode == TenantNetbirdGroupMode.ReadOnly)
                return Results.BadRequest(new { error = "Tenant NetBird group is read-only." });

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenantId,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "NETWORK_PEER_UPDATE",
                ResourceType = "NetworkPeer",
                ResourceId = id,
                IpAddress = actor.IpAddress,
                Result = "PENDING"
            });

            var updated = await netbird.UpdatePeerAsync(id, request);

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenantId,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "NETWORK_PEER_UPDATE",
                ResourceType = "NetworkPeer",
                ResourceId = id,
                IpAddress = actor.IpAddress,
                Result = "SUCCESS"
            });

            return Results.Ok(updated);
        }).RequireRateLimiting("api").RequireAuthorization("CpAdminOrAbove");

        // ── F3: Accessible Peers ────────────────────────────────────────────

        // GET /network/peers/{id}/accessible -- peers accessible to this peer
        app.MapGet("/network/peers/{id}/accessible", async (
            string id,
            INetbirdClient netbird,
            TenantNetworkService networkService,
            TenantContext tenant,
            ITenantRepository tenants,
            int? targetTenantId = null) =>
        {
            var resolution = await TenantTargetResolver.ResolveAsync(tenant, targetTenantId, tenants);
            if (!resolution.IsSuccess) return resolution.ToErrorResult();
            var tenantId = resolution.TenantId!.Value;

            var peer = await netbird.GetPeerByIdAsync(id);
            if (peer is null)
                return Results.NotFound();

            var tenantGroup = await networkService.GetTenantGroupAsync(tenantId);
            if (tenantGroup is null || !peer.Groups.Any(g => g.Id == tenantGroup.NetbirdGroupId))
                return Results.Forbid();

            var accessible = await netbird.GetAccessiblePeersAsync(id);
            return Results.Ok(accessible);
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");

        // ── G1: Network Visibility (read-only, cached) ─────────────────────

        // GET /network/routes -- list all Netbird routes
        app.MapGet("/network/routes", async (
            INetbirdClient netbird,
            IMemoryCache cache) =>
        {
            var routes = await cache.GetOrCreateAsync("netbird:routes", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
                return await netbird.GetRoutesAsync();
            });
            return Results.Ok(routes);
        }).RequireRateLimiting("api").RequireAuthorization("CpAdminOrAbove");

        // GET /network/policies -- list all Netbird policies
        app.MapGet("/network/policies", async (
            INetbirdClient netbird,
            IMemoryCache cache) =>
        {
            var policies = await cache.GetOrCreateAsync("netbird:policies", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
                return await netbird.GetPoliciesAsync();
            });
            return Results.Ok(policies);
        }).RequireRateLimiting("api").RequireAuthorization("CpAdminOrAbove");

        // GET /network/events -- list Netbird audit events
        app.MapGet("/network/events", async (
            INetbirdClient netbird,
            IMemoryCache cache) =>
        {
            var events = await cache.GetOrCreateAsync("netbird:events", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
                return await netbird.GetEventsAsync();
            });
            return Results.Ok(events);
        }).RequireRateLimiting("api").RequireAuthorization("SuperAdminOnly");

        // ── G2: Network Summary ─────────────────────────────────────────────

        // GET /network/summary -- aggregate network stats for the tenant
        app.MapGet("/network/summary", async (
            INetbirdClient netbird,
            TenantNetworkService networkService,
            TenantContext tenant,
            IActorContext actor,
            IMemoryCache cache,
            ITenantRepository tenants,
            int? targetTenantId = null) =>
        {
            var resolution = await TenantTargetResolver.ResolveAsync(tenant, targetTenantId, tenants);
            if (!resolution.IsSuccess) return resolution.ToErrorResult();
            var tenantId = resolution.TenantId!.Value;
            var isElevated = actor.Role is Role.SuperAdmin or Role.CpAdmin;
            var cacheKey = $"netbird:summary:{tenantId}:{isElevated}";

            var summary = await cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);

                try
                {
                    var tenantPeers = await networkService.GetTenantPeersAsync(tenantId);
                    var tenantPeersList = tenantPeers.ToList();

                    var tenantGroup = await networkService.GetTenantGroupAsync(tenantId);
                    var allKeys = tenantGroup is not null
                        ? (await netbird.GetSetupKeysAsync())
                            .Count(k => k.AutoGroups.Contains(tenantGroup.NetbirdGroupId) && k.Valid)
                        : 0;

                    int totalPeers;
                    int connectedPeers;
                    int routeCount;

                    if (isElevated)
                    {
                        var allPeers = await netbird.GetPeersAsync();
                        totalPeers = allPeers.Count();
                        connectedPeers = allPeers.Count(p => p.Connected);
                        var routes = await netbird.GetRoutesAsync();
                        routeCount = routes.Count();
                    }
                    else
                    {
                        totalPeers = tenantPeersList.Count;
                        connectedPeers = tenantPeersList.Count(p => p.Connected);
                        routeCount = 0;
                    }

                    return new NetworkSummaryResponse(
                        TotalPeers: totalPeers,
                        ConnectedPeers: connectedPeers,
                        TenantPeers: tenantPeersList.Count,
                        TenantConnectedPeers: tenantPeersList.Count(p => p.Connected),
                        SetupKeysActive: allKeys,
                        RouteCount: routeCount);
                }
                catch (Exception)
                {
                    return new NetworkSummaryResponse(
                        TotalPeers: 0,
                        ConnectedPeers: 0,
                        TenantPeers: 0,
                        TenantConnectedPeers: 0,
                        SetupKeysActive: 0,
                        RouteCount: 0);
                }
            });

            return Results.Ok(summary);
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");
    }
}
