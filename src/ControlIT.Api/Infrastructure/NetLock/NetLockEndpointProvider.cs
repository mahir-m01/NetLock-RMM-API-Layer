// ─────────────────────────────────────────────────────────────────────────────
// NetLockEndpointProvider.cs
// Pattern: Adapter — wraps NetLockSignalRService (a SignalR-specific class)
// behind the IEndpointProvider interface (a generic abstraction).
//
// WHY: ControlItFacade depends on IEndpointProvider, not on NetLockSignalRService.
// This means the facade never imports any SignalR types. If NetLock is replaced,
// only this adapter and SignalRCommandDispatcher need to change.
//
// Think of it like an electrical adapter: the appliance (ControlItFacade)
// plugs into the standard socket (IEndpointProvider), and the adapter handles
// the voltage conversion (SignalR protocol details).
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Infrastructure.NetLock;

using ControlIT.Api.Domain.Interfaces;

public class NetLockEndpointProvider : IEndpointProvider
{
    // We depend on the Singleton NetLockSignalRService directly here because
    // this provider is a thin adapter — it has no business logic of its own.
    private readonly NetLockSignalRService _signalR;
    private readonly ILogger<NetLockEndpointProvider> _logger;

    public NetLockEndpointProvider(NetLockSignalRService signalR,
        ILogger<NetLockEndpointProvider> logger)
    {
        _signalR = signalR;
        _logger = logger;
    }

    // Delegates to NetLockSignalRService.IsConnected.
    // Used by health checks and pre-flight validation in ControlItFacade.
    public bool IsConnected => _signalR.IsConnected;

    // Identifies this provider in health responses and logs.
    public string ProviderName => "NetLock";

    public async Task<string> DispatchCommandAsync(
        string deviceAccessKey, string commandJson, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        // Pre-flight check — give a clear error message instead of letting SignalR throw.
        if (!_signalR.IsConnected)
            throw new InvalidOperationException(
                "NetLock SignalR hub is not connected. Command dispatch unavailable.");

        // Delegate to the underlying service. All correlation logic is in NetLockSignalRService.
        return await _signalR.InvokeCommandAsync(deviceAccessKey, commandJson, timeout);
    }
}
