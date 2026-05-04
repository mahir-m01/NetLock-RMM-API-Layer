namespace ControlIT.Api.Tests.Unit;

using System.Reflection;
using ControlIT.Api.Application;
using ControlIT.Api.Common.Configuration;
using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.DTOs.Responses;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public class NetLockLiveBridgeTests
{
    [Fact]
    public async Task TickAsync_EmitsDeviceUpdatedThenOffline_FromLiveConnectedKeys()
    {
        var device = new Device
        {
            Id = 27,
            TenantId = 3,
            DeviceName = "lima-debian-test",
            AccessKey = "live-key",
            Platform = "Linux"
        };

        var netLock = new FakeNetLockAdminClient(
            Snapshot(new HashSet<string>(StringComparer.Ordinal) { "live-key" }),
            Snapshot(new HashSet<string>(StringComparer.Ordinal)));
        var publisher = new CapturingPublisher();
        var services = new ServiceCollection()
            .AddSingleton<IDeviceRepository>(new FakeDeviceRepository(device))
            .BuildServiceProvider();

        var bridge = new NetLockLiveBridge(
            services.GetRequiredService<IServiceScopeFactory>(),
            netLock,
            publisher,
            NullLogger<NetLockLiveBridge>.Instance,
            Options.Create(new NetLockLiveBridgeOptions()));

        await InvokeTickAsync(bridge);
        await InvokeTickAsync(bridge);

        Assert.Contains(publisher.Events, e =>
            e.Type == PushEventTypes.DeviceUpdated && e.TenantId == 3);
        Assert.Contains(publisher.Events, e =>
            e.Type == PushEventTypes.DeviceOffline && e.TenantId == 3);
    }

    [Fact]
    public async Task TickAsync_LoadsOneThousandDevicesInTwoPages()
    {
        var devices = Enumerable.Range(1, 1000)
            .Select(id => new Device
            {
                Id = id,
                TenantId = id <= 500 ? 10 : 11,
                DeviceName = $"device-{id}",
                AccessKey = $"key-{id}",
                Platform = "Linux"
            })
            .ToArray();

        var repo = new FakeDeviceRepository(devices);
        var netLock = new FakeNetLockAdminClient(Snapshot(new HashSet<string>(StringComparer.Ordinal)));
        var publisher = new CapturingPublisher();
        var services = new ServiceCollection()
            .AddSingleton<IDeviceRepository>(repo)
            .BuildServiceProvider();

        var bridge = new NetLockLiveBridge(
            services.GetRequiredService<IServiceScopeFactory>(),
            netLock,
            publisher,
            NullLogger<NetLockLiveBridge>.Instance,
            Options.Create(new NetLockLiveBridgeOptions { PageSize = 500 }));

        await InvokeTickAsync(bridge);

        Assert.Equal(1000, publisher.Events.Count(e => e.Type == PushEventTypes.DeviceUpdated));
        Assert.Equal([1, 2], repo.Filters.Select(f => f.Page).ToArray());
        Assert.All(repo.Filters, filter => Assert.Equal(500, filter.PageSize));
    }

    private static NetLockConnectedDevicesSnapshot Snapshot(IReadOnlySet<string> keys) =>
        new(keys, IsDegraded: false, DegradedReason: null, DateTimeOffset.UtcNow);

    private static async Task InvokeTickAsync(NetLockLiveBridge bridge)
    {
        var method = typeof(NetLockLiveBridge).GetMethod(
            "TickAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var task = (Task)method.Invoke(bridge, [CancellationToken.None])!;
        await task;
    }

    private sealed class CapturingPublisher : IPushEventPublisher
    {
        public List<PushEventEnvelope> Events { get; } = [];

        public ValueTask PublishAsync(PushEventEnvelope evt, CancellationToken ct = default)
        {
            Events.Add(evt);
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<PushEventEnvelope> SubscribeAsync(
            PushSubscriptionScope scope,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class FakeNetLockAdminClient : INetLockAdminClient
    {
        private readonly Queue<NetLockConnectedDevicesSnapshot> _snapshots;

        public FakeNetLockAdminClient(params NetLockConnectedDevicesSnapshot[] snapshots)
        {
            _snapshots = new Queue<NetLockConnectedDevicesSnapshot>(snapshots);
        }

        public Task<IReadOnlySet<string>> GetConnectedAccessKeysAsync(CancellationToken ct = default) =>
            Task.FromResult(_snapshots.Peek().ConnectedAccessKeys);

        public Task<NetLockConnectedDevicesSnapshot> GetConnectedDevicesSnapshotAsync(CancellationToken ct = default) =>
            Task.FromResult(_snapshots.Dequeue());
    }

    private sealed class FakeDeviceRepository : IDeviceRepository
    {
        private readonly IReadOnlyList<Device> _devices;

        public FakeDeviceRepository(params Device[] devices) => _devices = devices;

        public List<DeviceFilter> Filters { get; } = [];

        public Task<(IEnumerable<Device> Items, int TotalCount)> GetAllAsync(
            DeviceFilter filter,
            TenantContext tenantContext,
            CancellationToken cancellationToken = default)
        {
            Filters.Add(new DeviceFilter
            {
                Page = filter.Page,
                PageSize = filter.PageSize,
                SearchTerm = filter.SearchTerm,
                OnlineOnly = filter.OnlineOnly
            });

            var items = _devices
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToArray();

            return Task.FromResult(((IEnumerable<Device>)items, _devices.Count));
        }

        public Task<Device?> GetByIdAsync(
            int id,
            TenantContext tenantContext,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IEnumerable<string>> GetAllAccessKeysAsync(
            TenantContext tenantContext,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> GetAccessKeyAsync(
            int deviceId,
            TenantContext tenantContext,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
