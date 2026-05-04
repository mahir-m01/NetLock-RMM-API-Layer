// ─────────────────────────────────────────────────────────────────────────────
// SignalRCommandDispatcher.cs
// Pattern: Strategy (concrete) — implements ICommandDispatcher using SignalR.
//
// WHY this class exists (vs. calling NetLockSignalRService directly):
// ICommandDispatcher abstracts the transport layer. The application layer
// (ControlItFacade) calls ICommandDispatcher and never imports SignalR types.
// If we switch from SignalR to WebSocket or MQTT, only this class changes.
//
// Responsibilities:
//   - Validate and clamp timeout bounds
//   - Build the command JSON payload in NetLock's expected format
//   - Translate the raw output string into a typed CommandResult
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Infrastructure.NetLock;

using System.Text.Json;
using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;

public class SignalRCommandDispatcher : ICommandDispatcher
{
    // Depends on the concrete NetLockSignalRService because InvokeCommandAsync
    // is not part of IEndpointProvider (which is the higher-level abstraction).
    private readonly NetLockSignalRService _signalR;
    private readonly ILogger<SignalRCommandDispatcher> _logger;

    public SignalRCommandDispatcher(NetLockSignalRService signalR,
        ILogger<SignalRCommandDispatcher> logger)
    {
        _signalR = signalR;
        _logger = logger;
    }

    public async Task<CommandResult> DispatchAsync(
        string deviceAccessKey, CommandRequest request,
        CancellationToken cancellationToken = default)
    {
        // Clamp timeout: min=5 prevents instant timeouts from misconfigured clients;
        // max=120 prevents holding HTTP connections indefinitely.
        var timeoutSeconds = Math.Clamp(request.TimeoutSeconds, 5, 120);
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        // Build the command payload in NetLock's exact format (verified from CommandHub.cs):
        //   type           = 0  → remote shell (other values: 4=remote control, 5=check connection)
        //   wait_response  = true → hub will async await the device response and deliver it back
        //   powershell_code → used when shell is "powershell"; NetLock routes this to PS1
        //   command        → used when shell is "cmd" or "bash"; NetLock routes this to the shell
        //
        // NetLock Command fields (verified from Remote_Shell_Dialog.razor):
        //   type=0           → remote shell on all platforms
        //   powershell_code  → Base64-encoded shell command (ALL platforms use this field)
        //                      NetLock's agent decodes it and routes to the correct shell (bash/PS/cmd)
        //   command          → timeout integer as string (e.g. "30") — NOT the command text
        //   wait_response    → true so hub async-awaits the device response
        //
        // CRITICAL: the command MUST be Base64-encoded, otherwise the agent rejects it
        var encodedCommand = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(request.Command));

        var commandJson = JsonSerializer.Serialize(new
        {
            type = 0,
            wait_response = true,
            powershell_code = encodedCommand,       // Base64-encoded command text
            command = timeoutSeconds.ToString()  // timeout as string in the command field
        });

        _logger.LogInformation(
            "Dispatching command to device {DeviceId}, shell={Shell}, timeout={Timeout}s",
            request.DeviceId, request.Shell, timeoutSeconds);

        // Delegate to NetLockSignalRService. This will block (async await) until either:
        // - The device responds (→ returns output string)
        // - The timeout fires (→ throws TimeoutException)
        // - The SignalR connection drops (→ throws OperationCanceledException or similar)
        var rawResult = await _signalR.InvokeCommandAsync(deviceAccessKey, commandJson, timeout);

        // Status is set here; the caller (CommandEndpoints) writes the audit record.
        return new CommandResult
        {
            DeviceId = request.DeviceId.ToString(),
            Output = rawResult,
            ExecutedAt = DateTime.UtcNow,
            Status = "SUCCESS"
        };
    }
}
