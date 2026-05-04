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
using ControlIT.Api.Domain.DTOs.Responses;
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

        app.MapPost("/commands/batch", async (
            BatchCommandRequest req,
            ControlItFacade facade,
            IAuditService audit,
            TenantContext tenant,
            IActorContext actor) =>
        {
            req.TimeoutSeconds = Math.Clamp(req.TimeoutSeconds, 5, 120);

            var results = new List<BatchCommandDeviceResult>(req.DeviceIds.Count);
            foreach (var deviceId in req.DeviceIds)
            {
                var command = new CommandRequest
                {
                    DeviceId = deviceId,
                    Command = req.Command,
                    Shell = req.Shell,
                    TimeoutSeconds = req.TimeoutSeconds
                };

                results.Add(await ExecuteBatchItemAsync(command, facade, audit, tenant, actor));
            }

            var successCount = results.Count(r => r.Status == "SUCCESS");
            return Results.Ok(new BatchCommandResponse
            {
                RequestedCount = results.Count,
                SuccessCount = successCount,
                FailureCount = results.Count - successCount,
                Results = results
            });
        }).RequireRateLimiting("commands").RequireAuthorization("CanExecuteCommands");
    }

    private static async Task<BatchCommandDeviceResult> ExecuteBatchItemAsync(
        CommandRequest req,
        ControlItFacade facade,
        IAuditService audit,
        TenantContext tenant,
        IActorContext actor)
    {
        await RecordCommandAuditAsync(audit, tenant, actor, req.DeviceId, "PENDING");

        try
        {
            var result = await facade.ExecuteCommandAsync(req, tenant);
            await RecordCommandAuditAsync(audit, tenant, actor, req.DeviceId, "SUCCESS");

            return new BatchCommandDeviceResult
            {
                DeviceId = req.DeviceId,
                Status = result.Status,
                Message = "Command executed.",
                Output = result.Output,
                ExecutedAt = result.ExecutedAt
            };
        }
        catch (KeyNotFoundException ex)
        {
            await RecordCommandAuditAsync(audit, tenant, actor, req.DeviceId, "FAILURE", ex.Message);
            return BatchFailure(req.DeviceId, "FAILURE", "Device not found or not accessible.");
        }
        catch (TimeoutException ex)
        {
            await RecordCommandAuditAsync(audit, tenant, actor, req.DeviceId, "TIMEOUT", ex.Message);
            return BatchFailure(req.DeviceId, "TIMEOUT", "Device did not respond within the allowed time.");
        }
        catch (InvalidOperationException ex)
        {
            await RecordCommandAuditAsync(audit, tenant, actor, req.DeviceId, "FAILURE", ex.Message);
            var message = ex.Message.Contains("already pending", StringComparison.OrdinalIgnoreCase)
                ? "A command is already pending for this device."
                : "Command service temporarily unavailable.";
            return BatchFailure(req.DeviceId, "FAILURE", message);
        }
    }

    private static BatchCommandDeviceResult BatchFailure(
        int deviceId,
        string status,
        string message) =>
        new()
        {
            DeviceId = deviceId,
            Status = status,
            Message = message,
            ExecutedAt = DateTime.UtcNow
        };

    private static Task RecordCommandAuditAsync(
        IAuditService audit,
        TenantContext tenant,
        IActorContext actor,
        int deviceId,
        string result,
        string? errorMessage = null) =>
        audit.RecordAsync(new AuditEntry
        {
            TenantId = tenant.TenantId ?? 0,
            ActorKeyId = actor.UserId.ToString(),
            ActorEmail = actor.Email,
            Action = "COMMAND_EXECUTE",
            ResourceType = "Device",
            ResourceId = deviceId.ToString(),
            IpAddress = actor.IpAddress,
            Result = result,
            ErrorMessage = errorMessage
        });
}
