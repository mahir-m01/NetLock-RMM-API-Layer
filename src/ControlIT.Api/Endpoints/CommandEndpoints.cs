// ─────────────────────────────────────────────────────────────────────────────
// CommandEndpoints.cs
// Registers POST /commands/execute — the most critical and complex endpoint.
//
// WHY this endpoint holds an HTTP connection open up to 120s:
// The command flow is: HTTP request → SignalR message → device executes →
// SignalR response → HTTP response. The HTTP connection stays open the entire
// time. ASP.NET's async/await handles this efficiently — no thread is blocked.
//
// Audit strategy: Write PENDING before execution (captures the attempt even
// if the server crashes). Write SUCCESS/TIMEOUT/FAILURE after execution.
// This provides a complete audit trail for DPDP Act 2023 compliance.
//
// Rate limiting: "commands" policy (20/minute) — stricter than "api" (120/minute)
// to prevent command flooding while allowing normal API usage.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Endpoints;

using ControlIT.Api.Application;
using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;

public static class CommandEndpoints
{
    public static void Map(WebApplication app)
    {
        // POST /commands/execute
        // Body: { "deviceId": 1, "command": "ipconfig", "shell": "cmd", "timeoutSeconds": 30 }
        // CommandRequest is bound from the JSON request body automatically.
        app.MapPost("/commands/execute", async (
            CommandRequest req,
            ControlItFacade facade,
            IAuditService audit,
            TenantContext tenant,
            IActorContext actor) =>
        {
            // Clamp timeout: minimum 5s (avoid instant timeouts), maximum 120s (don't hold forever)
            // CommandRequest is a class (not a record), so we set the property directly.
            req.TimeoutSeconds = Math.Clamp(req.TimeoutSeconds, 5, 120);

            // Write PENDING audit entry BEFORE executing the command.
            // WHY before: if the server crashes during execution, we still have a record
            // that the command was attempted. "No audit trail" is worse than a PENDING entry.
            await audit.RecordAsync(new AuditEntry
            {
                TenantId = tenant.TenantId ?? 0,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "COMMAND_EXECUTE",
                ResourceType = "Device",
                ResourceId = req.DeviceId.ToString(),
                IpAddress = actor.IpAddress,
                Result = "PENDING"
            });

            try
            {
                // Execute the command — this awaits the SignalR response (up to TimeoutSeconds).
                var result = await facade.ExecuteCommandAsync(req, tenant);

                // Write SUCCESS audit entry after successful execution.
                await audit.RecordAsync(new AuditEntry
                {
                    TenantId = tenant.TenantId ?? 0,
                    ActorKeyId = actor.UserId.ToString(),
                    ActorEmail = actor.Email,
                    Action = "COMMAND_EXECUTE",
                    ResourceType = "Device",
                    ResourceId = req.DeviceId.ToString(),
                    IpAddress = actor.IpAddress,
                    Result = "SUCCESS"
                });

                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                await audit.RecordAsync(new AuditEntry
                {
                    TenantId = tenant.TenantId ?? 0,
                    ActorKeyId = actor.UserId.ToString(),
                    ActorEmail = actor.Email,
                    Action = "COMMAND_EXECUTE",
                    ResourceType = "Device",
                    ResourceId = req.DeviceId.ToString(),
                    IpAddress = actor.IpAddress,
                    Result = "FAILURE",
                    ErrorMessage = ex.Message
                });

                return Results.Problem(
                    detail: "Device not found or not accessible.",
                    statusCode: 404,
                    title: "Not Found");
            }
            catch (TimeoutException ex)
            {
                await audit.RecordAsync(new AuditEntry
                {
                    TenantId = tenant.TenantId ?? 0,
                    ActorKeyId = actor.UserId.ToString(),
                    ActorEmail = actor.Email,
                    Action = "COMMAND_EXECUTE",
                    ResourceType = "Device",
                    ResourceId = req.DeviceId.ToString(),
                    IpAddress = actor.IpAddress,
                    Result = "TIMEOUT",
                    ErrorMessage = ex.Message
                });

                return Results.Problem(
                    detail: "Device did not respond within the allowed time.",
                    statusCode: 504,
                    title: "Command Timeout");
            }
            catch (InvalidOperationException ex)
            {
                var isConflict = ex.Message.Contains("already pending", StringComparison.OrdinalIgnoreCase);
                var statusCode = isConflict ? 409 : 503;

                await audit.RecordAsync(new AuditEntry
                {
                    TenantId = tenant.TenantId ?? 0,
                    ActorKeyId = actor.UserId.ToString(),
                    ActorEmail = actor.Email,
                    Action = "COMMAND_EXECUTE",
                    ResourceType = "Device",
                    ResourceId = req.DeviceId.ToString(),
                    IpAddress = actor.IpAddress,
                    Result = "FAILURE",
                    ErrorMessage = ex.Message
                });

                return Results.Problem(
                    detail: isConflict
                        ? "A command is already pending for this device."
                        : "Command service temporarily unavailable.",
                    statusCode: statusCode,
                    title: isConflict ? "Conflict" : "Service Unavailable");
            }
        }).RequireRateLimiting("commands").RequireAuthorization("CanExecuteCommands");
    }
}
