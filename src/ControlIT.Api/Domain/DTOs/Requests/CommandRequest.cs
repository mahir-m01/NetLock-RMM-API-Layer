// CommandRequest.cs — Request DTO for POST /commands/execute.
// Carries the JSON body sent by the dashboard to the command endpoint.
// Not a domain model — no database mapping or business logic.

namespace ControlIT.Api.Domain.DTOs.Requests;

/// <summary>
/// The JSON body sent by the dashboard to POST /commands/execute.
/// Validated and clamped before being passed to ControlItFacade.
/// </summary>
public class CommandRequest
{
    // The integer primary key of the device to execute the command on.
    // ControlIT looks up the device's access_key from this ID (enforcing tenant scope).
    public int DeviceId { get; set; }

    // The shell command string to execute on the remote device.
    // Example: "ipconfig /all" or "systemctl status nginx"
    public string Command { get; set; } = string.Empty;

    // Which shell to use on the remote device.
    // "cmd"        = Windows Command Prompt (default)
    // "powershell" = Windows PowerShell
    // "bash"       = Linux/macOS bash
    public string Shell { get; set; } = "cmd";

    /// <summary>
    /// How long to wait for a response from the device before giving up.
    /// Validated: min 5s, max 120s. Defaults to 30s.
    /// Clamped in CommandEndpoints before passing to the facade.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
