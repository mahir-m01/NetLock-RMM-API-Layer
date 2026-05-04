namespace ControlIT.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ControlIT.Api.Application;
using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.DTOs.Responses;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using ControlIT.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

[Collection("Database")]
public class MinimalApiValidationTests
{
    private readonly ControlItWebApplicationFactory _factory;

    public MinimalApiValidationTests(MySqlContainerFixture dbFixture)
    {
        _factory = new ControlItWebApplicationFactory
        {
            DatabaseConnectionString = dbFixture.ConnectionString
        };
    }

    public static TheoryData<CommandRequest> InvalidCommandRequests => new()
    {
        new CommandRequest { DeviceId = 27, Command = "", Shell = "cmd", TimeoutSeconds = 30 },
        new CommandRequest { DeviceId = 27, Command = "whoami", Shell = "zsh", TimeoutSeconds = 30 },
        new CommandRequest { DeviceId = 27, Command = "whoami", Shell = "cmd", TimeoutSeconds = 4 },
        new CommandRequest { DeviceId = 0, Command = "whoami", Shell = "cmd", TimeoutSeconds = 30 }
    };

    public static TheoryData<BatchCommandRequest> InvalidBatchCommandRequests => new()
    {
        new BatchCommandRequest { DeviceIds = [], Command = "whoami", Shell = "bash", TimeoutSeconds = 30 },
        new BatchCommandRequest { DeviceIds = [27, 27], Command = "whoami", Shell = "bash", TimeoutSeconds = 30 },
        new BatchCommandRequest { DeviceIds = Enumerable.Range(1, 26).ToList(), Command = "whoami", Shell = "bash", TimeoutSeconds = 30 },
        new BatchCommandRequest { DeviceIds = [0], Command = "whoami", Shell = "bash", TimeoutSeconds = 30 },
        new BatchCommandRequest { DeviceIds = [27], Command = "", Shell = "bash", TimeoutSeconds = 30 },
        new BatchCommandRequest { DeviceIds = [27], Command = "whoami", Shell = "zsh", TimeoutSeconds = 30 }
    };

    [Theory]
    [MemberData(nameof(InvalidCommandRequests))]
    [Trait("Category", "Integration")]
    public async Task ExecuteCommand_WithInvalidBody_Returns400_BeforeDispatchOrAudit(CommandRequest request)
    {
        var audit = new Mock<IAuditService>(MockBehavior.Strict);
        var dispatcher = new Mock<ICommandDispatcher>(MockBehavior.Strict);
        using var factory = CreateFactory(audit: audit, dispatcher: dispatcher);
        var client = CreateClientWithToken(factory, Role.Technician, tenantId: 1);

        var response = await client.PostAsJsonAsync("/commands/execute", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        audit.Verify(a => a.RecordAsync(It.IsAny<AuditEntry>()), Times.Never);
        dispatcher.Verify(
            d => d.DispatchAsync(
                It.IsAny<string>(),
                It.IsAny<CommandRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [MemberData(nameof(InvalidBatchCommandRequests))]
    [Trait("Category", "Integration")]
    public async Task BatchCommand_WithInvalidBody_Returns400_BeforeDispatchOrAudit(BatchCommandRequest request)
    {
        var audit = new Mock<IAuditService>(MockBehavior.Strict);
        var dispatcher = new Mock<ICommandDispatcher>(MockBehavior.Strict);
        using var factory = CreateFactory(audit: audit, dispatcher: dispatcher);
        var client = CreateClientWithToken(factory, Role.Technician, tenantId: 1);

        var response = await client.PostAsJsonAsync("/commands/batch", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        audit.Verify(a => a.RecordAsync(It.IsAny<AuditEntry>()), Times.Never);
        dispatcher.Verify(
            d => d.DispatchAsync(
                It.IsAny<string>(),
                It.IsAny<CommandRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task BatchCommand_WithValidBody_FansOutExistingCommandPath_AndAuditsPerDevice()
    {
        var device27 = MakeDevice(27, "access-27");
        var device28 = MakeDevice(28, "access-28");
        var audit = new Mock<IAuditService>(MockBehavior.Strict);
        var dispatcher = new Mock<ICommandDispatcher>(MockBehavior.Strict);
        var devices = new Mock<IDeviceRepository>(MockBehavior.Strict);
        var netLock = new Mock<INetLockAdminClient>(MockBehavior.Strict);
        var push = new Mock<IPushEventPublisher>(MockBehavior.Strict);

        audit.Setup(a => a.RecordAsync(It.IsAny<AuditEntry>())).Returns(Task.CompletedTask);
        devices.Setup(d => d.GetByIdAsync(27, It.IsAny<TenantContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(device27);
        devices.Setup(d => d.GetByIdAsync(28, It.IsAny<TenantContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(device28);
        netLock.Setup(n => n.GetConnectedAccessKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { device27.AccessKey, device28.AccessKey });
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<CommandRequest>(), It.IsAny<CancellationToken>()))
            .Returns<string, CommandRequest, CancellationToken>((_, command, _) => Task.FromResult(new CommandResult
            {
                DeviceId = command.DeviceId.ToString(),
                Output = $"ok-{command.DeviceId}",
                Status = "SUCCESS",
                ExecutedAt = DateTime.UtcNow
            }));
        push.Setup(p => p.PublishAsync(It.IsAny<PushEventEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        using var factory = CreateFactory(
            audit: audit,
            dispatcher: dispatcher,
            devices: devices,
            netLock: netLock,
            push: push,
            endpointIsConnected: true);
        var client = CreateClientWithToken(factory, Role.Technician, tenantId: 1);

        var response = await client.PostAsJsonAsync("/commands/batch", new BatchCommandRequest
        {
            DeviceIds = [27, 28],
            Command = "whoami",
            Shell = "bash",
            TimeoutSeconds = 30
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BatchCommandResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.RequestedCount);
        Assert.Equal(2, body.SuccessCount);
        Assert.All(body.Results, r => Assert.Equal("SUCCESS", r.Status));
        audit.Verify(a => a.RecordAsync(It.IsAny<AuditEntry>()), Times.Exactly(4));
        dispatcher.Verify(
            d => d.DispatchAsync(
                It.IsAny<string>(),
                It.IsAny<CommandRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetDevices_WithInvalidQuery_Returns400_BeforeFacadeReads()
    {
        var devices = new Mock<IDeviceRepository>(MockBehavior.Strict);
        var netLock = new Mock<INetLockAdminClient>(MockBehavior.Strict);
        using var factory = CreateFactory(devices: devices, netLock: netLock);
        var client = CreateClientWithToken(factory, Role.ClientAdmin, tenantId: 1);
        var searchTerm = new string('x', 201);

        var response = await client.GetAsync($"/devices?page=0&pageSize=101&searchTerm={searchTerm}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        devices.Verify(
            d => d.GetAllAsync(
                It.IsAny<DeviceFilter>(),
                It.IsAny<TenantContext>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        netLock.Verify(n => n.GetConnectedAccessKeysAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private WebApplicationFactory<TenantContext> CreateFactory(
        Mock<IAuditService>? audit = null,
        Mock<ICommandDispatcher>? dispatcher = null,
        Mock<IDeviceRepository>? devices = null,
        Mock<INetLockAdminClient>? netLock = null,
        Mock<IPushEventPublisher>? push = null,
        bool endpointIsConnected = false)
    {
        var endpoint = new Mock<IEndpointProvider>(MockBehavior.Strict);
        endpoint.SetupGet(e => e.IsConnected).Returns(endpointIsConnected);
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAuditService>();
                services.RemoveAll<ICommandDispatcher>();
                services.RemoveAll<IDeviceRepository>();
                services.RemoveAll<IEndpointProvider>();
                services.RemoveAll<INetLockAdminClient>();
                services.RemoveAll<IPushEventPublisher>();

                services.AddSingleton((audit ?? new Mock<IAuditService>(MockBehavior.Strict)).Object);
                services.AddSingleton((dispatcher ?? new Mock<ICommandDispatcher>(MockBehavior.Strict)).Object);
                services.AddSingleton((devices ?? new Mock<IDeviceRepository>(MockBehavior.Strict)).Object);
                services.AddSingleton(endpoint.Object);
                services.AddSingleton((netLock ?? new Mock<INetLockAdminClient>(MockBehavior.Strict)).Object);
                services.AddSingleton((push ?? new Mock<IPushEventPublisher>(MockBehavior.Strict)).Object);
            });
        });
    }

    private static Device MakeDevice(int id, string accessKey) =>
        new()
        {
            Id = id,
            TenantId = 1,
            DeviceName = $"device-{id}",
            AccessKey = accessKey,
            LastAccess = DateTime.UtcNow
        };

    private HttpClient CreateClientWithToken(
        WebApplicationFactory<TenantContext> factory,
        Role role,
        int? tenantId = null)
    {
        var client = factory.CreateClient();
        var token = _factory.IssueToken(role, tenantId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
