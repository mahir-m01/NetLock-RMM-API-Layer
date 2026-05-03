namespace ControlIT.Api.Tests.Unit;

using System.Text.Json;
using ControlIT.Api.Application;
using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.DTOs.Responses;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

[Trait("Category", "Unit")]
public class CommandStatusPushTests
{
    [Fact]
    public async Task ExecuteCommandAsync_PublishesPendingAndSuccess_WithoutAccessKey()
    {
        var events = new List<PushEventEnvelope>();
        var device = MakeDevice();
        var facade = MakeFacade(
            device,
            events,
            connectedKeys: new HashSet<string>(StringComparer.Ordinal) { device.AccessKey },
            dispatcher: request => Task.FromResult(new CommandResult
            {
                DeviceId = device.Id.ToString(),
                Status = "SUCCESS",
                Output = "ok",
                ExecutedAt = DateTime.UtcNow
            }));

        await facade.ExecuteCommandAsync(MakeRequest(), MakeTenant());

        Assert.Equal(["PENDING", "SUCCESS"], Statuses(events));
        Assert.All(events, e => Assert.Equal(device.TenantId, e.TenantId));
        var json = JsonSerializer.Serialize(events, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain(device.AccessKey, json);
        Assert.DoesNotContain("accessKey", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteCommandAsync_PublishesTimeout_WhenDispatcherTimesOut()
    {
        var events = new List<PushEventEnvelope>();
        var device = MakeDevice();
        var facade = MakeFacade(
            device,
            events,
            connectedKeys: new HashSet<string>(StringComparer.Ordinal) { device.AccessKey },
            dispatcher: _ => throw new TimeoutException("timed out"));

        await Assert.ThrowsAsync<TimeoutException>(() =>
            facade.ExecuteCommandAsync(MakeRequest(), MakeTenant()));

        Assert.Equal(["PENDING", "TIMEOUT"], Statuses(events));
    }

    [Fact]
    public async Task ExecuteCommandAsync_PublishesFailure_WhenDeviceOffline()
    {
        var events = new List<PushEventEnvelope>();
        var device = MakeDevice();
        var facade = MakeFacade(
            device,
            events,
            connectedKeys: new HashSet<string>(StringComparer.Ordinal),
            dispatcher: _ => throw new InvalidOperationException("should not dispatch"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            facade.ExecuteCommandAsync(MakeRequest(), MakeTenant()));

        Assert.Equal(["PENDING", "FAILURE"], Statuses(events));
    }

    private static ControlItFacade MakeFacade(
        Device device,
        List<PushEventEnvelope> events,
        IReadOnlySet<string> connectedKeys,
        Func<CommandRequest, Task<CommandResult>> dispatcher)
    {
        var devices = new Mock<IDeviceRepository>(MockBehavior.Strict);
        devices
            .Setup(d => d.GetByIdAsync(device.Id, It.IsAny<TenantContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        var netLock = new Mock<INetLockAdminClient>(MockBehavior.Strict);
        netLock
            .Setup(n => n.GetConnectedAccessKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connectedKeys);

        var commands = new Mock<ICommandDispatcher>(MockBehavior.Strict);
        commands
            .Setup(c => c.DispatchAsync(
                device.AccessKey,
                It.IsAny<CommandRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, CommandRequest, CancellationToken>((_, req, _) => dispatcher(req));

        var endpoint = new Mock<IEndpointProvider>(MockBehavior.Strict);
        endpoint.SetupGet(e => e.IsConnected).Returns(true);

        var push = new Mock<IPushEventPublisher>(MockBehavior.Strict);
        push
            .Setup(p => p.PublishAsync(It.IsAny<PushEventEnvelope>(), It.IsAny<CancellationToken>()))
            .Callback<PushEventEnvelope, CancellationToken>((evt, _) => events.Add(evt))
            .Returns(ValueTask.CompletedTask);

        return new ControlItFacade(
            devices.Object,
            Mock.Of<IEventRepository>(),
            Mock.Of<ITenantRepository>(),
            commands.Object,
            endpoint.Object,
            Mock.Of<IAuditService>(),
            netLock.Object,
            Mock.Of<INetbirdMappingRepository>(),
            push.Object,
            NullLogger<ControlItFacade>.Instance);
    }

    private static Device MakeDevice() =>
        new()
        {
            Id = 27,
            TenantId = 8,
            DeviceName = "lima-debian-test",
            AccessKey = "secret-access-key",
            LastAccess = DateTime.UtcNow
        };

    private static CommandRequest MakeRequest() =>
        new()
        {
            DeviceId = 27,
            Command = "whoami",
            Shell = "bash",
            TimeoutSeconds = 30
        };

    private static TenantContext MakeTenant()
    {
        var actor = new Mock<IActorContext>();
        actor.SetupGet(a => a.Role).Returns(Role.ClientAdmin);
        actor.SetupGet(a => a.TenantId).Returns(8);
        return new TenantContext(actor.Object);
    }

    private static string[] Statuses(IEnumerable<PushEventEnvelope> events) =>
        events
            .Where(e => e.Type == PushEventTypes.CommandStatus)
            .Select(e => Assert.IsType<CommandStatusPushPayload>(e.Payload).Status)
            .ToArray();
}
