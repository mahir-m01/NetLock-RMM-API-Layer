// ─────────────────────────────────────────────────────────────────────────────
// IEndpointProvider.cs
// Pattern: Adapter — wraps NetLock-specific SignalR details behind a generic
// "endpoint provider" concept. The ControlItFacade doesn't import any SignalR
// types — it calls this interface.
//
// WHY: Abstracts all NetLock-specific logic. If NetLock is replaced, only the
// NetLockEndpointProvider implementation changes — the application layer
// is completely unaffected. This is the Adapter pattern: converts the NetLock
// SignalR API into the shape that ControlIT's application layer expects.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Domain.Interfaces;

/// <summary>
/// Abstracts all NetLock-specific logic. If NetLock is replaced, only this
/// implementation changes — the application layer is unaffected.
/// </summary>
public interface IEndpointProvider
{
    // Sends a JSON-encoded command to the named device and returns the raw output.
    // timeout controls how long to wait before throwing TimeoutException.
    Task<string> DispatchCommandAsync(
        string deviceAccessKey, string commandJson, TimeSpan timeout,
        CancellationToken cancellationToken = default);

    // True when the underlying transport (SignalR) is in Connected state.
    // Used by health checks and pre-flight validation before dispatch.
    bool IsConnected { get; }

    // Human-readable name of the provider (e.g., "NetLock").
    // Used in health responses and logs.
    string ProviderName { get; }
}
