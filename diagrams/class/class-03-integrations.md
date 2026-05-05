# CLASS 03 - NetLock, NetBird, Push Integrations

```mermaid
classDiagram
    direction LR

    class ICommandDispatcher {
        <<interface>>
        DispatchAsync(deviceAccessKey, request, ct)
    }

    class SignalRCommandDispatcher {
        BuildCommandJson()
        DispatchAsync()
    }

    class NetLockSignalRService {
        StartAsync()
        InvokeCommandAsync()
        ReceiveClientResponseRemoteShell()
        pendingCommands
    }

    class INetLockAdminClient {
        <<interface>>
        GetConnectedAccessKeysAsync()
        GetConnectedDevicesSnapshotAsync()
    }

    class NetLockAdminClient {
        GetConnectedAccessKeysAsync()
        GetConnectedDevicesSnapshotAsync()
    }

    class NetLockLiveBridge {
        TickAsync()
        LoadAllDevicesAsync()
        PublishHealthAsync()
    }

    class IPushEventPublisher {
        <<interface>>
        PublishAsync()
        SubscribeAsync()
    }

    class PushEventHub {
        PublishAsync()
        SubscribeAsync()
        CanReceive()
    }

    class INetbirdClient {
        <<interface>>
        GetPeersAsync()
        GetGroupsAsync()
        CreateSetupKeyAsync()
        DeleteSetupKeyAsync()
        CreatePolicyAsync()
    }

    class NetbirdApiClient {
        HTTP bearer token
        NetBird REST adapter
    }

    class TenantNetworkService {
        EnsureTenantGroupAsync()
        BindTenantGroupAsync()
        GetTenantPeersAsync()
    }

    class NetbirdMappingRepository {
        DevicePeerMap
        TenantGroupMap
    }

    ICommandDispatcher <|.. SignalRCommandDispatcher
    SignalRCommandDispatcher --> NetLockSignalRService
    INetLockAdminClient <|.. NetLockAdminClient
    NetLockLiveBridge --> INetLockAdminClient
    NetLockLiveBridge --> IPushEventPublisher
    IPushEventPublisher <|.. PushEventHub

    INetbirdClient <|.. NetbirdApiClient
    TenantNetworkService --> INetbirdClient
    TenantNetworkService --> NetbirdMappingRepository
```

## Boundary Notes

- NetLock SignalR command payload uses backend-only device access key.
- `NetLockLiveBridge` translates NetLock live state into ControlIT push events.
- `PushEventHub` filters by tenant server-side before writing SSE frames.
- `TenantNetworkService` keeps NetBird group ownership separate from device inventory.
