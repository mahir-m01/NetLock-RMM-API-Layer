// ─────────────────────────────────────────────────────────────────────────────
// ICommandDispatcher.cs
// Pattern: Strategy — different command transport mechanisms (SignalR today,
// potentially WebSocket or HTTP in future) implement the same interface.
//
// WHY: The application layer calls ICommandDispatcher without knowing it's
// talking to SignalR. If NetLock changes its transport protocol, only
// SignalRCommandDispatcher changes — ControlItFacade is unaffected.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Domain.Interfaces;

using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.Models;

public interface ICommandDispatcher
{
    // Sends a shell command to a remote device and waits for the response.
    // deviceAccessKey: NetLock's unique identifier for the device connection.
    // Throws TimeoutException if the device doesn't respond within request.TimeoutSeconds.
    // Throws InvalidOperationException if the hub is not connected (503).
    Task<CommandResult> DispatchAsync(
        string deviceAccessKey, CommandRequest request,
        CancellationToken cancellationToken = default);
}
