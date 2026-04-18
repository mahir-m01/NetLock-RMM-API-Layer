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
            HttpContext ctx) =>
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
                ActorKeyId = GetActorKeyId(ctx),   // First 16 chars of the API key hash
                Action = "COMMAND_EXECUTE",
                ResourceType = "Device",
                ResourceId = req.DeviceId.ToString(),
                IpAddress = ctx.Connection.RemoteIpAddress?.ToString(),
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
                    ActorKeyId = GetActorKeyId(ctx),
                    Action = "COMMAND_EXECUTE",
                    ResourceType = "Device",
                    ResourceId = req.DeviceId.ToString(),
                    IpAddress = ctx.Connection.RemoteIpAddress?.ToString(),
                    Result = "SUCCESS"
                });

                return Results.Ok(result);
            }
            catch (TimeoutException ex)
            {
                // Device didn't respond in time — write TIMEOUT audit entry.
                await audit.RecordAsync(new AuditEntry
                {
                    TenantId = tenant.TenantId ?? 0,
                    ActorKeyId = GetActorKeyId(ctx),
                    Action = "COMMAND_EXECUTE",
                    ResourceType = "Device",
                    ResourceId = req.DeviceId.ToString(),
                    IpAddress = ctx.Connection.RemoteIpAddress?.ToString(),
                    Result = "TIMEOUT",
                    ErrorMessage = ex.Message
                });

                // 504 Gateway Timeout — the device (downstream) didn't respond in time.
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 504,
                    title: "Command Timeout");
            }
            catch (InvalidOperationException ex)
            {
                // Hub not connected, device not found, or duplicate command in flight.
                await audit.RecordAsync(new AuditEntry
                {
                    TenantId = tenant.TenantId ?? 0,
                    ActorKeyId = GetActorKeyId(ctx),
                    Action = "COMMAND_EXECUTE",
                    ResourceType = "Device",
                    ResourceId = req.DeviceId.ToString(),
                    IpAddress = ctx.Connection.RemoteIpAddress?.ToString(),
                    Result = "FAILURE",
                    ErrorMessage = ex.Message
                });

                // 503 Service Unavailable — the hub/device is not available.
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 503,
                    title: "Service Unavailable");
            }
        }).RequireRateLimiting("commands").RequireAuthorization("CanExecuteCommands");
    }

    // Returns the first 16 characters of the SHA-256 hash of the API key.
    // WHY not the full hash: The full hash is 64 chars; 16 is enough for identification.
    // WHY not the raw key: Never log or store the raw API key — log the hash prefix only.
    // WHY static: This is a pure function with no state — static avoids unnecessary instance.
    private static string GetActorKeyId(HttpContext ctx)
    {
        var rawKey = ctx.Request.Headers["x-api-key"].FirstOrDefault() ?? string.Empty;
        if (string.IsNullOrEmpty(rawKey)) return "unknown";
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(rawKey)));
        return hash[..16].ToLowerInvariant();
    }
}
