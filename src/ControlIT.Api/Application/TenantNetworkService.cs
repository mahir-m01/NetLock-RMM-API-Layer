namespace ControlIT.Api.Application;

using System.Collections.Concurrent;
using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.Extensions.Caching.Memory;

public class TenantNetworkService
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();

    private readonly INetbirdClient _netbird;
    private readonly INetbirdMappingRepository _mappingRepo;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TenantNetworkService> _logger;

    public TenantNetworkService(
        INetbirdClient netbird,
        INetbirdMappingRepository mappingRepo,
        IMemoryCache cache,
        ILogger<TenantNetworkService> logger)
    {
        _netbird = netbird;
        _mappingRepo = mappingRepo;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Returns the tenant's Netbird group ID, creating the group and isolation policy if needed.
    /// </summary>
    public async Task<string> EnsureTenantGroupAsync(int tenantId, CancellationToken ct = default)
    {
        var cacheKey = TenantGroupCacheKey(tenantId);

        var cached = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);

            var semaphore = _locks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(ct);
            try
            {
                // Double-check: another thread may have created the group while we waited
                var existing = await _mappingRepo.GetTenantGroupAsync(tenantId, ct);
                if (existing is not null)
                {
                    var group = await _netbird.GetGroupByIdAsync(existing.NetbirdGroupId, ct);
                    if (group is not null)
                        return existing.NetbirdGroupId;

                    if (!existing.ControlItManaged)
                    {
                        throw new InvalidOperationException(
                            "Mapped external NetBird group no longer exists.");
                    }

                    // Group was deleted externally — remove stale DB record before recreating
                    _logger.LogWarning(
                        "Netbird group {GroupId} for tenant {TenantId} no longer exists. Recreating.",
                        existing.NetbirdGroupId, tenantId);

                    await _mappingRepo.DeleteTenantGroupAsync(tenantId, ct);
                }

                var groupName = $"controlit-tenant-{tenantId}";
                var groups = await _netbird.GetGroupsAsync(ct);
                var newGroup = groups.FirstOrDefault(g => g.Name == groupName)
                    ?? await _netbird.CreateGroupAsync(groupName, ct: ct);

                var policyName = $"controlit-isolation-tenant-{tenantId}";
                var policies = await _netbird.GetPoliciesAsync(ct);
                var policy = policies.FirstOrDefault(p => p.Name == policyName)
                    ?? await _netbird.CreatePolicyAsync(new CreatePolicyRequest(
                    Name: policyName,
                    Description: $"Auto-created by ControlIT. Allows intra-tenant traffic for tenant {tenantId}.",
                    Enabled: true,
                    Rules:
                    [
                        new PolicyRuleRequest(
                            Name: "allow-intra-tenant",
                            Action: "accept",
                            Bidirectional: true,
                            Protocol: "all",
                            Sources: [newGroup.Id],
                            Destinations: [newGroup.Id])
                    ]), ct);

                var mapping = new TenantNetbirdGroup
                {
                    TenantId = tenantId,
                    NetbirdGroupId = newGroup.Id,
                    NetbirdGroupName = groupName,
                    IsolationPolicyId = policy.Id,
                    GroupMode = TenantNetbirdGroupMode.Managed,
                    ControlItManaged = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _mappingRepo.CreateTenantGroupAsync(mapping, ct);

                _logger.LogInformation(
                    "Created Netbird group {GroupId} and isolation policy {PolicyId} for tenant {TenantId}",
                    newGroup.Id, policy.Id, tenantId);

                return newGroup.Id;
            }
            finally
            {
                semaphore.Release();
            }
        });

        return cached!;
    }

    public async Task<TenantNetbirdGroup?> GetTenantGroupAsync(int tenantId, CancellationToken ct = default)
    {
        return await _mappingRepo.GetTenantGroupAsync(tenantId, ct);
    }

    public async Task<TenantNetbirdGroup> BindTenantGroupAsync(
        int tenantId,
        string groupId,
        string mode,
        CancellationToken ct = default)
    {
        if (mode is not TenantNetbirdGroupMode.External and not TenantNetbirdGroupMode.ReadOnly)
            throw new ArgumentException("Bind mode must be external or read_only.", nameof(mode));

        var group = await _netbird.GetGroupByIdAsync(groupId, ct)
            ?? throw new KeyNotFoundException("NetBird group not found.");

        var existingByGroup = await _mappingRepo.GetTenantGroupByNetbirdGroupIdAsync(groupId, ct);
        if (existingByGroup is not null && existingByGroup.TenantId != tenantId)
            throw new InvalidOperationException("NetBird group is already bound to another tenant.");

        var now = DateTime.UtcNow;
        var mapping = new TenantNetbirdGroup
        {
            TenantId = tenantId,
            NetbirdGroupId = group.Id,
            NetbirdGroupName = group.Name,
            IsolationPolicyId = string.Empty,
            GroupMode = mode,
            ControlItManaged = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _mappingRepo.UpsertTenantGroupAsync(mapping, ct);
        _cache.Remove(TenantGroupCacheKey(tenantId));
        _cache.Remove(TenantPeersCacheKey(tenantId));

        return mapping;
    }

    /// <summary>
    /// Returns peers that belong to this tenant's Netbird group.
    /// </summary>
    public async Task<IEnumerable<NetbirdPeer>> GetTenantPeersAsync(int tenantId, CancellationToken ct = default)
    {
        var cacheKey = TenantPeersCacheKey(tenantId);

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);

            var tenantGroup = await _mappingRepo.GetTenantGroupAsync(tenantId, ct);
            if (tenantGroup is null)
                return Enumerable.Empty<NetbirdPeer>();

            var allPeers = await _netbird.GetPeersAsync(ct);
            return allPeers.Where(p =>
                p.Groups.Any(g => g.Id == tenantGroup.NetbirdGroupId));
        }) ?? Enumerable.Empty<NetbirdPeer>();
    }

    private static string TenantGroupCacheKey(int tenantId) => $"netbird:tenant_group:{tenantId}";
    private static string TenantPeersCacheKey(int tenantId) => $"netbird:tenant_peers:{tenantId}";
}
