# CLASS 01 - Overall Architecture

```mermaid
classDiagram
    direction LR

    class ControlITWeb {
        Next.js dashboard
        memory access token
        httpOnly refresh cookie
        SSE subscription
    }

    class ControlITApi {
        ASP.NET Minimal APIs
        JWT auth
        RBAC policies
        tenant scope
        rate limits
    }

    class ApplicationLayer {
        ControlItFacade
        AuthService
        TenantNetworkService
        TenantTargetResolver
        RoleCeiling
    }

    class PersistenceLayer {
        Dapper repositories
        EF Core DbContext
        MySQL connection factory
    }

    class PushLayer {
        PushEventHub
        NetLockLiveBridge
        dashboard stream
        sync stream
    }

    class NetLockBoundary {
        pre-existing NetLock install
        MySQL read-only tables
        SignalR commandHub
        Admin API
    }

    class NetBirdBoundary {
        NetBird Management API
        groups
        setup keys
        peers
        routes
        events
    }

    class ControlITTables {
        controlit_users
        controlit_refresh_tokens
        controlit_audit_log
        controlit_device_netbird_map
        controlit_tenant_netbird_group
    }

    ControlITWeb --> ControlITApi : REST + SSE
    ControlITApi --> ApplicationLayer : endpoint orchestration
    ApplicationLayer --> PersistenceLayer : repositories
    ApplicationLayer --> PushLayer : publish events
    PersistenceLayer --> NetLockBoundary : Dapper reads only
    PersistenceLayer --> ControlITTables : EF Core owns
    ApplicationLayer --> NetLockBoundary : SignalR/Admin calls
    ApplicationLayer --> NetBirdBoundary : HTTP adapter
    PushLayer --> NetLockBoundary : live state snapshot
```

## Notes

- ControlIT is API/dashboard layer, not NetLock fork.
- NetLock tables stay vendor-owned and read-only to ControlIT.
- NetBird state lives in NetBird; ControlIT stores tenant/device mappings only.
- Dashboard live state comes from SSE push. No polling fallback.
