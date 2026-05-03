namespace ControlIT.Api.Tests.Unit;

using System.Reflection;
using ControlIT.Api.Application;
using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.DTOs.Responses;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
            NullLogger<NetLockLiveBridge>.Instance);

        await InvokeTickAsync(bridge);
        await InvokeTickAsync(bridge);

        Assert.Contains(publisher.Events, e =>
            e.Type == PushEventTypes.DeviceUpdated && e.TenantId == 3);
        Assert.Contains(publisher.Events, e =>
            e.Type == PushEventTypes.DeviceOffline && e.TenantId == 3);
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
        private readonly Device _device;

        public FakeDeviceRepository(Device device) => _device = device;

        public Task<(IEnumerable<Device> Items, int TotalCount)> GetAllAsync(
            DeviceFilter filter,
            TenantContext tenantContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(((IEnumerable<Device>)new[] { _device }, 1));

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
