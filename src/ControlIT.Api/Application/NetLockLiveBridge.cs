namespace ControlIT.Api.Application;

using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.DTOs.Responses;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;

public sealed class NetLockLiveBridge : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int PageSize = 500;
    private const string ComponentName = "netlock-live-bridge";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INetLockAdminClient _netLock;
    private readonly IPushEventPublisher _publisher;
    private readonly ILogger<NetLockLiveBridge> _logger;
    private readonly Dictionary<int, bool> _lastOnlineByDeviceId = new();
    private DateTimeOffset _lastHealthPublished = DateTimeOffset.MinValue;
    private bool _wasDegraded;
    private bool _hasBaseline;

    public NetLockLiveBridge(
        IServiceScopeFactory scopeFactory,
        INetLockAdminClient netLock,
        IPushEventPublisher publisher,
        ILogger<NetLockLiveBridge> logger)
    {
        _scopeFactory = scopeFactory;
        _netLock = netLock;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NetLock live bridge tick failed.");
                await PublishHealthAsync("degraded", "netlock_live_bridge_tick_failed", force: true, stoppingToken);
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    internal async Task TickAsync(CancellationToken ct)
    {
        var snapshot = await _netLock.GetConnectedDevicesSnapshotAsync(ct);
        if (snapshot.IsDegraded)
        {
            await PublishHealthAsync("degraded", snapshot.DegradedReason, force: false, ct);
            return;
        }

        if (_wasDegraded)
            await PublishHealthAsync("healthy", null, force: true, ct);
        else
            await PublishHealthAsync("healthy", null, force: false, ct);

        var devices = await LoadAllDevicesAsync(ct);
        var dashboardByTenant = BuildDashboardByTenant(devices, snapshot.ConnectedAccessKeys);

        foreach (var device in devices)
        {
            var isOnline = snapshot.ConnectedAccessKeys.Contains(device.AccessKey);
            var changed = _lastOnlineByDeviceId.TryGetValue(device.Id, out var previous)
                && previous != isOnline;
            dashboardByTenant.TryGetValue(device.TenantId, out var dashboard);

            _lastOnlineByDeviceId[device.Id] = isOnline;

            if (!_hasBaseline)
            {
                await _publisher.PublishAsync(
                    PushEventFactory.Device(PushEventTypes.DeviceUpdated, device, isOnline, dashboard), ct);
                continue;
            }

            if (changed)
            {
                var type = isOnline ? PushEventTypes.DeviceOnline : PushEventTypes.DeviceOffline;
                await _publisher.PublishAsync(PushEventFactory.Device(type, device, isOnline, dashboard), ct);
            }
        }

        _hasBaseline = true;
    }

    private static IReadOnlyDictionary<int, DashboardStatsPushPayload> BuildDashboardByTenant(
        IReadOnlyList<Device> devices,
        IReadOnlySet<string> connectedKeys)
    {
        return devices
            .GroupBy(device => device.TenantId)
            .ToDictionary(
                group => group.Key,
                group => new DashboardStatsPushPayload(
                    TotalDevices: group.Count(),
                    OnlineDevices: group.Count(device => connectedKeys.Contains(device.AccessKey)),
                    TotalTenants: 0,
                    TotalEvents: 0,
                    CriticalAlerts: 0));
    }

    private async Task<IReadOnlyList<Device>> LoadAllDevicesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
        var tenant = new TenantContext(SystemActorContext.Instance);
        var all = new List<Device>();

        for (var page = 1; ; page++)
        {
            var (items, total) = await devices.GetAllAsync(
                new DeviceFilter { Page = page, PageSize = PageSize },
                tenant,
                ct);

            all.AddRange(items);
            if (all.Count >= total || total == 0)
                return all;
        }
    }

    private async Task PublishHealthAsync(
        string status,
        string? reason,
        bool force,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (!force && now - _lastHealthPublished < TimeSpan.FromSeconds(15))
            return;

        _lastHealthPublished = now;
        _wasDegraded = status == "degraded";
        if (status == "degraded")
            _logger.LogWarning("NetLock live bridge status {Status}: {Reason}", status, reason ?? "ok");

        await _publisher.PublishAsync(
            PushEventFactory.SystemHealth(ComponentName, status, reason), ct);
    }

    private sealed class SystemActorContext : IActorContext
    {
        public static readonly SystemActorContext Instance = new();
        public int UserId => 0;
        public Role Role => Role.SuperAdmin;
        public int? TenantId => null;
        public IReadOnlyList<int> AssignedClients => [];
        public string? IpAddress => null;
        public string Email => "system@controlit.local";
    }
}
