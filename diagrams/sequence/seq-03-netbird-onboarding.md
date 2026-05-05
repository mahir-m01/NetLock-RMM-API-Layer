# SEQ3 - NetBird Tenant Onboarding

```mermaid
sequenceDiagram
    autonumber
    actor Admin as SuperAdmin/CpAdmin
    actor TenantMember as Tenant member
    participant Web as Network Page
    participant API as NetworkEndpoints
    participant Resolver as TenantTargetResolver
    participant Service as TenantNetworkService
    participant MapRepo as NetbirdMappingRepository
    participant DeviceRepo as DeviceRepository
    participant NetBird as NetBird Management API
    participant Audit as AuditService

    Admin->>Web: Select tenant if elevated
    Web->>API: /network/* ?targetTenantId=N
    API->>Resolver: Resolve tenant target
    alt Elevated role missing targetTenantId
        Resolver-->>API: 400
        API-->>Web: targetTenantId required
    else Scoped role cross-tenant target
        Resolver-->>API: 403
        API-->>Web: forbidden
    else Valid tenant
        Resolver-->>API: tenantId
    end

    Admin->>Web: Bind existing NetBird group
    Web->>API: POST /network/tenant-group
    API->>Service: BindTenantGroupAsync(groupId, external/read_only)
    Service->>NetBird: Validate group exists
    Service->>MapRepo: Upsert tenant group mapping
    API-->>Web: TenantNetbirdGroup

    Admin->>Web: Create setup key
    Web->>API: POST /network/setup-keys
    API->>Service: EnsureTenantGroupAsync()
    alt Group read_only
        API-->>Web: 400 read-only group
    else Writable group
        API->>Audit: SETUP_KEY_CREATE PENDING
        API->>NetBird: Create setup key with tenant auto_group
        NetBird-->>API: Raw key
        API->>Audit: SETUP_KEY_CREATE SUCCESS
        API-->>Web: Raw key once
    end

    TenantMember->>Web: List setup keys
    Web->>API: GET /network/setup-keys
    API->>NetBird: List setup keys
    API-->>Web: Keys with key="[redacted]"

    Admin->>Web: Link peer to device
    Web->>API: POST /network/peers/{peerId}/link
    API->>NetBird: Get peer and verify tenant group
    API->>DeviceRepo: GetByIdAsync(deviceId, tenant scope)
    API->>MapRepo: Ensure peer/device not already mapped
    API->>MapRepo: Create device-peer mapping
    API-->>Web: Mapping with NetBird IP
```

## Notes

- ControlIT uses NetBird API only.
- Setup key list never returns raw key.
- Peer link validates both peer tenant group and NetLock device tenant scope.
