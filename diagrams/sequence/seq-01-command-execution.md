# SEQ1 - Command Execution

```mermaid
sequenceDiagram
    autonumber
    actor Operator
    participant Web as Commands Page
    participant API as CommandEndpoints
    participant Auth as JWT Policy + Rate Limit
    participant Audit as AuditService
    participant Facade as ControlItFacade
    participant Devices as MySqlDeviceRepository
    participant Live as NetLockAdminClient
    participant Dispatch as SignalRCommandDispatcher
    participant SignalR as NetLockSignalRService
    participant Hub as NetLock commandHub
    participant Agent as NetLock Agent
    participant Push as PushEventHub

    Operator->>Web: Select device(s), shell, command
    Web->>API: POST /commands/execute or /commands/batch
    API->>Auth: CanExecuteCommands + partitioned rate limit
    API->>API: Validate shell, timeout, device ids
    API->>Audit: PENDING

    loop each device in batch or once for single command
        API->>Facade: ExecuteCommandAsync(deviceId)
        Facade->>Devices: GetByIdAsync(deviceId, TenantContext)
        Devices-->>Facade: Device including backend-only access_key
        Facade->>Live: GetConnectedAccessKeysAsync()
        alt Device offline
            Facade->>Push: command.status FAILURE device_offline
            Facade-->>API: 503 result
        else Device online
            Facade->>Push: command.status PENDING
            Facade->>Dispatch: DispatchAsync(accessKey, request)
            Dispatch->>SignalR: InvokeCommandAsync(accessKey, encoded command)
            SignalR->>Hub: MessageReceivedFromWebconsole(root entity)
            Hub->>Agent: Remote shell command
            Agent-->>Hub: Output
            Hub-->>SignalR: ReceiveClientResponseRemoteShell(device_id, output)
            SignalR-->>Dispatch: Output
            Dispatch-->>Facade: CommandResult SUCCESS
            Facade->>Push: command.status SUCCESS
            Facade-->>API: 200 result
        end
        API->>Audit: SUCCESS / TIMEOUT / FAILURE
    end

    API-->>Web: Single result or BatchCommandResponse
```

## Guarantees

- Client sends `deviceId`, never NetLock access key.
- Backend resolves device inside tenant scope before reading access key.
- Pending command guard prevents concurrent command overwrite per device.
- Batch command returns per-device result; each item gets audit and push status.
