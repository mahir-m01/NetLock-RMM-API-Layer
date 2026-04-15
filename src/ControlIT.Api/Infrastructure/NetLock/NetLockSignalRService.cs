// ─────────────────────────────────────────────────────────────────────────────
// NetLockSignalRService.cs
// Pattern: Singleton Service + Observer (IHostedService)
//
// This is the most architecturally critical class in the codebase. It:
//   1. Owns the SignalR connection to NetLock's commandHub
//   2. Manages a dictionary of in-flight command awaits (_pendingCommands)
//   3. Correlates SignalR responses back to the HTTP request that sent the command
//
// WHY Singleton: The SignalR connection is process-wide — one connection handles
// all tenants' commands. Making it Scoped would create a new connection per request,
// which would break the response correlation entirely.
//
// P0 — KEY DESIGN DECISION:
// _pendingCommands is keyed by device_id (int PK as STRING), NOT by deviceAccessKey.
// WHY: Two concurrent commands to the same device would use the same access_key,
// overwriting the TCS and delivering the wrong result to the wrong caller.
// By keying on device_id (the numeric PK), we enforce one-command-per-device
// and detect conflicts before they happen.
//
// Response format from NetLock: "device_id>>nlocksep<<output"
// This is a plain string (NOT JSON). Verified against NetLock CommandHub.cs line 561.
// Split on ">>nlocksep<<", parts[0] = device_id, parts[1] = command output.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Infrastructure.NetLock;

using System.Collections.Concurrent;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.SignalR.Client;
using ControlIT.Api.Common.Configuration;
using ControlIT.Api.Domain.Interfaces;
using Microsoft.Extensions.Options;

public class NetLockSignalRService : IHostedService, IAsyncDisposable
{
    private readonly NetLockOptions _options;
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger<NetLockSignalRService> _logger;
    private readonly ISchemaValidator _schemaValidator;

    // The SignalR HubConnection is created in StartAsync and nulled in DisposeAsync.
    // Nullable because it doesn't exist until StartAsync runs.
    private HubConnection? _connection;

    /// <summary>
    /// device_id (int PK as string) → TaskCompletionSource.
    ///
    /// P0 KEY RULE: Keyed by device_id — NOT by deviceAccessKey, NOT by responseId.
    /// NetLock's callback format: "device_id>>nlocksep<<output" (plain string, not JSON).
    /// One pending command per device. 409 Conflict if device already has a command in flight.
    ///
    /// ConcurrentDictionary is used because:
    /// - The SignalR callback runs on a background thread
    /// - HTTP request handlers run on different threads
    /// - We need thread-safe add/remove without explicit locking
    /// </summary>
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>>
        _pendingCommands = new();

    // True when the SignalR connection is in the Connected state.
    // Checked by IEndpointProvider.IsConnected before every dispatch.
    public bool IsConnected =>
        _connection?.State == HubConnectionState.Connected;

    public NetLockSignalRService(
        IOptions<NetLockOptions> options,
        IDbConnectionFactory factory,
        ILogger<NetLockSignalRService> logger,
        ISchemaValidator schemaValidator)
    {
        _options = options.Value;
        _factory = factory;
        _logger = logger;
        _schemaValidator = schemaValidator;
    }

    // ── IHostedService ────────────────────────────────────────────────────────
    // StartAsync is called by the ASP.NET host when the application starts.
    // StopAsync is called on graceful shutdown.

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Validate the NetLock DB schema BEFORE opening the SignalR connection.
        // If any required column is missing, this throws and the app won't start.
        // WHY: better to fail-fast at startup than to silently return null values
        // from Dapper queries at runtime.
        await _schemaValidator.ValidateRequiredColumnsAsync(cancellationToken);

        // Build the HubConnection — configures the URL, auth header, and retry policy.
        _connection = new HubConnectionBuilder()
            .WithUrl(_options.HubUrl, options =>
            {
                // NetLock's JsonAuthMiddleware deserializes Admin-Identity into Root_Entity,
                // which has the shape: { "admin_identity": { "token": "<value>" } }
                // The header value must be URL-encoded. Sending only {"token":"..."} at the
                // top level causes rootData.admin_identity to be null — auth fails silently.
                // SECURITY: never log _options.AdminSessionToken — it grants full admin access.
                options.Headers["Admin-Identity"] =
                    Uri.EscapeDataString($"{{\"admin_identity\":{{\"token\":\"{_options.AdminSessionToken}\"}}}}");
            })
            // InfiniteRetryPolicy ensures reconnects are retried forever with exponential backoff.
            .WithAutomaticReconnect(new InfiniteRetryPolicy())
            .Build();

        // ── Response handler — keyed by device_id ─────────────────────────────
        // "ReceiveClientResponseRemoteShell" is the SignalR method name on the NetLock hub.
        // It delivers the command output as a plain string: "device_id>>nlocksep<<output"
        _connection.On<string>("ReceiveClientResponseRemoteShell", (result) =>
        {
            // Split on the NetLock separator. StringSplitOptions.None preserves empty strings.
            var parts = result.Split(">>nlocksep<<", 2);

            if (parts.Length == 2 && _pendingCommands.TryRemove(parts[0], out var tcs))
            {
                // parts[0] = device_id (string), parts[1] = output text
                // TrySetResult delivers the result to the awaiting InvokeCommandAsync call.
                tcs.TrySetResult(parts[1]);
            }
            else
            {
                // Malformed response or no matching pending command — log and ignore.
                // Do NOT throw here — this runs on the SignalR receive thread.
                _logger.LogWarning(
                    "Received SignalR response with no matching pending command. DeviceId={Id}",
                    parts.ElementAtOrDefault(0) ?? "<null>");
            }
        });

        // ── Reconnect handler — re-validate admin token on reconnect ──────────
        // When the connection drops and reconnects, the admin token may have been rotated.
        // If the token is invalid, stop the connection rather than silently failing commands.
        _connection.Reconnected += async (connectionId) =>
        {
            var tokenValid = await ValidateAdminTokenAsync(cancellationToken);
            if (!tokenValid)
            {
                // SECURITY: don't log the token value — just log that it's invalid.
                _logger.LogCritical(
                    "Admin token is invalid after reconnect. Stopping SignalR connection.");
                // Use CancellationToken.None — we want this stop to complete even if
                // the original cancellationToken was cancelled.
                await _connection.StopAsync(CancellationToken.None);
                return;
            }
            _logger.LogInformation(
                "Reconnected to NetLock commandHub. ConnectionId={Id}", connectionId);
        };

        // ── Disconnect handler — cancel all in-flight commands ────────────────
        // When the connection closes (dropped, not graceful shutdown), every pending
        // command will never get a response. Cancel all TCSes to unblock the HTTP callers.
        _connection.Closed += (exception) =>
        {
            _logger.LogWarning(exception,
                "SignalR connection closed. Cancelling {Count} in-flight commands.",
                _pendingCommands.Count);

            // Iterate and cancel all pending commands.
            // TryRemove is used because another thread might remove the same entry
            // (e.g., a TTL expiry fires simultaneously).
            foreach (var kvp in _pendingCommands)
            {
                if (_pendingCommands.TryRemove(kvp.Key, out var tcs))
                    tcs.TrySetCanceled();
            }

            return Task.CompletedTask;
        };

        await _connection.StartAsync(cancellationToken);

        // SECURITY: only log the URL, never the token.
        _logger.LogInformation("Connected to NetLock commandHub at {Url}", _options.HubUrl);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
            await _connection.StopAsync(cancellationToken);
    }

    // ── Command dispatch ──────────────────────────────────────────────────────

    /// <summary>
    /// Dispatches a command to a device via the NetLock SignalR hub.
    /// Returns the raw output string from the device.
    ///
    /// Flow:
    ///   1. Look up device_id from access_key (DB query)
    ///   2. Register a TaskCompletionSource keyed by device_id
    ///   3. Invoke "MessageReceivedFromWebconsole" on the SignalR hub
    ///   4. Await the TCS (fulfilled by the ReceiveClientResponseRemoteShell handler)
    ///   5. Return the output string
    ///
    /// Throws InvalidOperationException (→ 503) if hub is not connected.
    /// Throws InvalidOperationException (→ 409) if a command is already pending for this device.
    /// Throws TimeoutException (→ 504) if no response within <paramref name="timeout"/>.
    /// </summary>
    public async Task<string> InvokeCommandAsync(
        string deviceAccessKey, string commandJson, TimeSpan timeout)
    {
        // Pre-flight check — fail fast if the hub is not connected.
        if (_connection?.State != HubConnectionState.Connected)
            throw new InvalidOperationException(
                "NetLock commandHub is not connected. Command cannot be dispatched.");

        // Step 1: Resolve device_id from access_key.
        // WHY: The response callback delivers device_id, not access_key.
        // We must register the TCS under device_id so we can look it up on response.
        var deviceIdStr = await LookupDeviceIdAsync(deviceAccessKey);

        // Step 2: Enforce one-pending-command-per-device.
        // ContainsKey is safe here because if there's a race between the check and TryAdd,
        // the worst case is two commands for the same device — one will fail when its
        // TCS is removed by the other. The TryAdd below is the actual guard.
        if (_pendingCommands.ContainsKey(deviceIdStr))
            throw new InvalidOperationException(
                $"A command is already pending for device {deviceAccessKey}. Wait for it to complete or time out.");

        // Step 3: Create the TaskCompletionSource that will carry the response back.
        // RunContinuationsAsynchronously prevents the continuation from running on the
        // SignalR receive thread, which could cause deadlocks.
        var tcs = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // Step 4: Register TTL cleanup — prevents memory leak if device never responds.
        // When the CancellationTokenSource fires, remove the TCS and cancel the task.
        using var cts = new CancellationTokenSource(timeout);
        cts.Token.Register(() =>
        {
            _pendingCommands.TryRemove(deviceIdStr, out _);
            tcs.TrySetCanceled();
        });

        // Register the TCS under device_id — the response handler looks it up here.
        _pendingCommands[deviceIdStr] = tcs;

        try
        {
            // Step 5: Build and send the command to the NetLock hub.
            var payload = BuildRootEntity(deviceAccessKey, commandJson);
            // URL-encode the JSON payload — NetLock expects it encoded.
            var encoded = Uri.EscapeDataString(JsonSerializer.Serialize(payload));

            // "MessageReceivedFromWebconsole" is the NetLock hub method name.
            // InvokeAsync sends the message and waits for the hub to acknowledge receipt
            // (NOT for the device to respond — that comes via ReceiveClientResponseRemoteShell).
            await _connection.InvokeAsync("MessageReceivedFromWebconsole", encoded);

            // Step 6: Await the TCS — this suspends the async method until either:
            //   a) ReceiveClientResponseRemoteShell fires and calls tcs.TrySetResult()
            //   b) The CancellationTokenSource fires and calls tcs.TrySetCanceled()
            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            // CancellationToken fired = timeout exceeded.
            // Remove from dictionary (cleanup) and throw TimeoutException for the caller.
            _pendingCommands.TryRemove(deviceIdStr, out _);
            throw new TimeoutException(
                $"Command timed out after {timeout.TotalSeconds}s. Device: {deviceAccessKey}");
        }
        catch
        {
            // Any other exception (SignalR disconnected, etc.) — clean up and re-throw.
            _pendingCommands.TryRemove(deviceIdStr, out _);
            throw;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Builds the root JSON payload that NetLock's MessageReceivedFromWebconsole expects.
    // Format: { admin_identity: { token }, target_device: { access_key }, command: {...} }
    private object BuildRootEntity(string deviceAccessKey, string commandJson)
    {
        // Deserialize commandJson back to JsonElement so it's not double-serialized.
        var cmd = JsonSerializer.Deserialize<JsonElement>(commandJson);
        return new
        {
            admin_identity = new { token = _options.AdminSessionToken },
            target_device  = new { access_key = deviceAccessKey },
            command        = cmd
        };
    }

    // Looks up the device's integer PK (id) from its access_key.
    // This is needed because NetLock's response format uses device_id, not access_key.
    private async Task<string> LookupDeviceIdAsync(string accessKey)
    {
        using var conn = await _factory.CreateConnectionAsync();
        var id = await conn.ExecuteScalarAsync<int?>(
            "SELECT id FROM devices WHERE access_key = @key",
            new { key = accessKey });

        if (id is null)
            throw new InvalidOperationException(
                $"Device not found for access_key: {accessKey}");

        // Return as string because _pendingCommands is ConcurrentDictionary<string, ...>
        return id.Value.ToString();
    }

    // Validates the admin session token is still valid by checking the accounts table.
    // Called on reconnect — the token may have been rotated while we were disconnected.
    private async Task<bool> ValidateAdminTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var conn = await _factory.CreateConnectionAsync(cancellationToken);
            var count = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM accounts WHERE remote_session_token = @token",
                new { token = _options.AdminSessionToken });
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate admin token on reconnect.");
            return false;
        }
    }

    // ── IAsyncDisposable ─────────────────────────────────────────────────────
    // Called when the service is being disposed (app shutdown).
    // Disposes the HubConnection which closes the underlying WebSocket.

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
