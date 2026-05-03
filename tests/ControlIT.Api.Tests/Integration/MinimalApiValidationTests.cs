namespace ControlIT.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ControlIT.Api.Application;
using ControlIT.Api.Domain.DTOs.Requests;
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
        Mock<INetLockAdminClient>? netLock = null)
    {
        var endpoint = new Mock<IEndpointProvider>(MockBehavior.Strict);
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAuditService>();
                services.RemoveAll<ICommandDispatcher>();
                services.RemoveAll<IDeviceRepository>();
                services.RemoveAll<IEndpointProvider>();
                services.RemoveAll<INetLockAdminClient>();

                services.AddSingleton((audit ?? new Mock<IAuditService>(MockBehavior.Strict)).Object);
                services.AddSingleton((dispatcher ?? new Mock<ICommandDispatcher>(MockBehavior.Strict)).Object);
                services.AddSingleton((devices ?? new Mock<IDeviceRepository>(MockBehavior.Strict)).Object);
                services.AddSingleton(endpoint.Object);
                services.AddSingleton((netLock ?? new Mock<INetLockAdminClient>(MockBehavior.Strict)).Object);
            });
        });
    }

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
