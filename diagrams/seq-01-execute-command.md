# SEQ1 — POST /commands/execute: Full Command Dispatch Flow

**Scope:** End-to-end flow for dispatching a remote command to a managed endpoint via `POST /commands/execute`.
Covers API key validation, tenant resolution, SignalR dispatch, response correlation by `responseId`, audit logging, and timeout handling.
**Phase:** Phase 1 — NetLock RMM integration.
**Source:** architect_api_layer.md (post-evaluation), source-of-truth.md.

---

```mermaid
sequenceDiagram
    autonumber

    actor Dashboard as Dashboard<br/>(Next.js)
    participant MW as ApiKeyMiddleware
    participant TC as TenantContext<br/>(Scoped)
    participant CE as CommandEndpoints
    participant Facade as ControlItFacade
    participant Disp as SignalRCommandDispatcher
    participant SRSvc as NetLockSignalRService<br/>(Singleton)
    participant Audit as AuditService
    participant DB as MySQL<br/>(controlit_audit_log)
    participant Hub as NetLock commandHub<br/>(SignalR)
    participant Agent as NetLock Agent<br/>(on device)

    %% ─────────────────────────────────────────
    %% 1. Request enters — middleware pipeline
    %% ─────────────────────────────────────────

    Dashboard->>MW: POST /commands/execute<br/>x-api-key: <key><br/>{ deviceAccessKey, command }

    MW->>DB: SELECT tenant_id, is_active<br/>FROM controlit_tenant_api_keys<br/>WHERE api_key_hash = SHA256(key)
    DB-->>MW: { tenant_id: 3, is_active: true }

    Note over MW,TC: tenant_id is NEVER trusted from the client.<br/>Always derived server-side here.

    MW->>TC: tenantContext.TenantId = 3
    MW->>CE: next(context) — request continues

    %% ─────────────────────────────────────────
    %% 2. Endpoint handler
    %% ─────────────────────────────────────────

    CE->>CE: Validate request body<br/>(deviceAccessKey required)
    CE->>Facade: ExecuteCommandAsync(deviceAccessKey, commandJson, tenantId)

    %% ─────────────────────────────────────────
    %% 3. Audit — record attempt before dispatch
    %% ─────────────────────────────────────────

    Facade->>Audit: RecordAsync(AuditEntry { action: "command.execute",<br/>tenantId: 3, deviceKey: "...", timestamp: UtcNow })
    Audit->>DB: INSERT INTO controlit_audit_log (...)
    DB-->>Audit: ok

    Note over Audit: Audit write happens before dispatch.<br/>Never throws — logs failure and continues.

    %% ─────────────────────────────────────────
    %% 4. Command dispatch via SignalR
    %% ─────────────────────────────────────────

    Facade->>Disp: DispatchAsync(deviceAccessKey, commandJson)
    Disp->>SRSvc: InvokeCommandAsync(deviceAccessKey, commandJson)

    SRSvc->>SRSvc: Resolve device_id: SELECT id FROM devices<br/>WHERE access_key = @deviceAccessKey AND tenant_id = @tenantId

    alt _pendingCommands already has device_id entry
        SRSvc-->>Disp: throw InvalidOperationException("Command already pending for this device")
        Disp-->>Facade: propagates
        Facade-->>CE: propagates
        CE-->>Dashboard: HTTP 409 Conflict { error: "A command is already pending for this device" }
    end

    SRSvc->>SRSvc: tcs = new TaskCompletionSource<string>()<br/>_pendingCommands[device_id] = tcs<br/>cts = new CancellationTokenSource(config.CommandTimeoutSeconds)

    Note over SRSvc: Key = device_id (integer PK), NOT responseId.<br/>NetLock generates responseId internally — ControlIT never receives it.<br/>One pending command per device enforced here.

    SRSvc->>SRSvc: BuildRootEntity(deviceAccessKey, commandJson)<br/>→ { admin_identity: { token }, target_device: { access_key },<br/>command: { type: 0, wait_response: true, powershell_code: "..." } }

    SRSvc->>Hub: InvokeAsync("MessageReceivedFromWebconsole", encodedMessage)
    Hub->>Agent: Forward command to target device

    %% ─────────────────────────────────────────
    %% 5a. Happy path — response received in time
    %% ─────────────────────────────────────────

    Agent-->>Hub: Command output
    Hub-->>SRSvc: On("ReceiveClientResponseRemoteShell", "device_id>>nlocksep<<output")

    SRSvc->>SRSvc: Parse device_id from "device_id>>nlocksep<<output"<br/>_pendingCommands.TryRemove(device_id) → tcs<br/>tcs.TrySetResult(output)

    SRSvc-->>Disp: return output string
    Disp-->>Facade: return CommandResult { output }
    Facade-->>CE: CommandResult

    CE-->>Dashboard: HTTP 200 OK<br/>{ output, tenantId }

    %% ─────────────────────────────────────────
    %% 5b. Timeout path — no response within TTL
    %% ─────────────────────────────────────────

    alt Command times out (config.CommandTimeoutSeconds — default 30s)
        SRSvc->>SRSvc: cts.Token fires<br/>_pendingCommands.TryRemove(device_id)<br/>tcs.TrySetCanceled()
        SRSvc-->>Disp: throw TimeoutException("Command timed out after Xs")
        Disp-->>Facade: TimeoutException propagates
        Facade-->>CE: TimeoutException
        CE-->>Dashboard: HTTP 504 Gateway Timeout<br/>{ error: "Command timed out after Xs" }
    end

    %% ─────────────────────────────────────────
    %% 5c. SignalR disconnected path
    %% ─────────────────────────────────────────

    alt SignalR hub not connected at dispatch time
        SRSvc-->>Disp: throw InvalidOperationException("Not connected to NetLock commandHub")
        Disp-->>Facade: propagates
        Facade-->>CE: propagates
        CE-->>Dashboard: HTTP 503 Service Unavailable<br/>{ error: "SignalR hub not connected" }
    end
```

---

## Design Decisions

| Concern | Decision |
|---|---|
| API key validation | SHA-256 hash compared against `controlit_tenant_api_keys`. Result cached for 5 minutes per key. |
| Tenant isolation | `TenantContext.TenantId` derived exclusively from API key lookup in `ApiKeyMiddleware`. Never accepted from request body or query params. |
| Audit timing | Intent logged before dispatch. Outcome logged after. `RecordAsync` never throws — audit failure does not block command execution. |
| `_pendingCommands` keying | Keyed by `device_id` (integer PK). NetLock's callback delivers `"device_id>>nlocksep<<output"` — `responseId` is generated internally by NetLock and never returned to ControlIT. One command per device enforced — 409 Conflict if device already has a command in flight. |
| Command timeout | Server-side only — `NetLock:CommandTimeoutSeconds: 30` in `appsettings.json`. Not accepted from the request body. NetLock's internal cleanup runs at 5 minutes (`MAX_COMMAND_AGE_MINUTES`). |
| Rate limiting | `POST /commands/execute` — fixed window: 20 req/min, queue 0. Exceeded: HTTP 429. |
| SignalR reconnect | `InfiniteRetryPolicy` with exponential backoff capped at 60–90s. In-flight commands at disconnect time expire via their CTS. |
