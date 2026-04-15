// ─────────────────────────────────────────────────────────────────────────────
// NetLockEndpointProvider.cs
// Pattern: Adapter — wraps NetLockSignalRService behind the IEndpointProvider interface.
//
// WHY: ControlItFacade depends on IEndpointProvider, not on NetLockSignalRService.
// The facade imports no SignalR types. If NetLock is replaced, only this adapter
// and SignalRCommandDispatcher need to change.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Infrastructure.NetLock;

using ControlIT.Api.Domain.Interfaces;

public class NetLockEndpointProvider : IEndpointProvider
{
    // Depends on the concrete NetLockSignalRService — this adapter has no business logic
    // of its own, so an additional interface abstraction is unnecessary.
    private readonly NetLockSignalRService _signalR;
    private readonly ILogger<NetLockEndpointProvider> _logger;

    public NetLockEndpointProvider(NetLockSignalRService signalR,
        ILogger<NetLockEndpointProvider> logger)
    {
        _signalR = signalR;
        _logger = logger;
    }

    public bool IsConnected => _signalR.IsConnected;

    public string ProviderName => "NetLock";

    public async Task<string> DispatchCommandAsync(
        string deviceAccessKey, string commandJson, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        // Pre-flight check produces a clear error rather than letting SignalR throw an opaque one.
        if (!_signalR.IsConnected)
            throw new InvalidOperationException(
                "NetLock SignalR hub is not connected. Command dispatch unavailable.");

        // All correlation logic lives in NetLockSignalRService.
        return await _signalR.InvokeCommandAsync(deviceAccessKey, commandJson, timeout);
    }
}
