namespace ControlIT.Api.Tests.Unit;

using System.Text.Json;
using ControlIT.Api.Application;
using ControlIT.Api.Domain.DTOs.Responses;
using ControlIT.Api.Domain.Models;
using Xunit;

[Trait("Category", "Unit")]
public class PushEventContractTests
{
    [Fact]
    public void PushEventTypes_DefinesRequiredVersionedEventNames()
    {
        Assert.Contains(PushEventTypes.DeviceOnline, PushEventTypes.All);
        Assert.Contains(PushEventTypes.DeviceOffline, PushEventTypes.All);
        Assert.Contains(PushEventTypes.DeviceUpdated, PushEventTypes.All);
        Assert.Contains(PushEventTypes.CommandStatus, PushEventTypes.All);
        Assert.Contains(PushEventTypes.NetbirdPeerUpdated, PushEventTypes.All);
        Assert.Contains(PushEventTypes.SystemHealthUpdated, PushEventTypes.All);
        Assert.Equal(1, PushEventEnvelope.CurrentVersion);
    }

    [Fact]
    public void DeviceEvents_DoNotSerializeAccessKeys()
    {
        const string secret = "super-secret-access-key";
        var device = new Device
        {
            Id = 27,
            TenantId = 4,
            DeviceName = "lima-debian-test",
            AccessKey = secret,
            Platform = "Linux",
            OperatingSystem = "Debian",
            AgentVersion = "1.0.0",
            IpAddressInternal = "10.0.0.5",
            IpAddressExternal = "203.0.113.10",
            CpuUsage = 12,
            RamUsage = 34,
            LastAccess = DateTime.UtcNow
        };

        var evt = PushEventFactory.Device(PushEventTypes.DeviceOnline, device, isOnline: true);
        var json = JsonSerializer.Serialize(evt, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("emittedAt", json);
        Assert.DoesNotContain("occurredAt", json);
        Assert.DoesNotContain(secret, json);
        Assert.DoesNotContain("accessKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("access_key", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TenantFiltering_AllowsOnlyScopedTenantAndSystemHealth()
    {
        var tenantScope = new PushSubscriptionScope(IsAllTenants: false, TenantId: 10);
        var own = PushEventEnvelope.Create(PushEventTypes.DeviceOnline, 10, new { deviceId = 1 });
        var other = PushEventEnvelope.Create(PushEventTypes.DeviceOnline, 11, new { deviceId = 2 });
        var system = PushEventFactory.SystemHealth("netlock-live-bridge", "healthy", null);

        Assert.True(PushEventHub.CanReceive(tenantScope, own));
        Assert.False(PushEventHub.CanReceive(tenantScope, other));
        Assert.True(PushEventHub.CanReceive(tenantScope, system));
    }

    [Fact]
    public void TenantFiltering_AllTenantScopeReceivesTenantEvents()
    {
        var allTenantScope = new PushSubscriptionScope(IsAllTenants: true, TenantId: null);
        var tenantEvent = PushEventEnvelope.Create(PushEventTypes.DeviceOffline, 42, new { deviceId = 7 });

        Assert.True(PushEventHub.CanReceive(allTenantScope, tenantEvent));
    }

    [Fact]
    public async Task PushEventHub_BuffersOneThousandDeviceEventsForSubscriber()
    {
        var hub = new PushEventHub();
        var received = new List<PushEventEnvelope>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var readTask = Task.Run(async () =>
        {
            await foreach (var evt in hub.SubscribeAsync(
                new PushSubscriptionScope(IsAllTenants: true, TenantId: null),
                cts.Token))
            {
                received.Add(evt);
                if (received.Count == 1000)
                    break;
            }
        }, cts.Token);

        await Task.Delay(25, cts.Token);
        for (var i = 1; i <= 1000; i++)
        {
            await hub.PublishAsync(
                PushEventEnvelope.Create(PushEventTypes.DeviceUpdated, 1, new { deviceId = i }),
                cts.Token);
        }

        await readTask;
        Assert.Equal(1000, received.Count);
    }
}
