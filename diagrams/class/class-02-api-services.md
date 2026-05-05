# CLASS 02 - API Services and Security

```mermaid
classDiagram
    direction TB

    class AuthEndpoints {
        Login
        Refresh
        Logout
        ChangePassword
        ResetPassword
    }

    class UserEndpoints {
        ListUsers
        CreateUser
        PatchUser
        DeactivateUser
        ForcePasswordReset
    }

    class CommandEndpoints {
        ExecuteCommand
        ExecuteBatchCommand
    }

    class NetworkEndpoints {
        ListPeers
        BindTenantGroup
        ListSetupKeys
        CreateSetupKey
        EnrolPeer
        LinkPeerToDevice
    }

    class AuthService {
        LoginAsync()
        RefreshAsync()
        LogoutAsync()
        ChangePasswordAsync()
        ResetPasswordAsync()
    }

    class JwtService {
        IssueAccessToken()
        ValidateToken()
    }

    class RoleCeiling {
        CanManage(actorRole, targetRole)
    }

    class TenantTargetResolver {
        ResolveAsync(tenant, targetTenantId)
    }

    class ControlItFacade {
        GetDashboardSummaryAsync()
        GetDevicesAsync()
        ExecuteCommandAsync()
        GetEventsAsync()
    }

    class TenantNetworkService {
        EnsureTenantGroupAsync()
        BindTenantGroupAsync()
        GetTenantPeersAsync()
    }

    class AuditService {
        RecordAsync()
        QueryAsync()
    }

    class Repositories {
        UserRepository
        RefreshTokenRepository
        PasswordResetTokenRepository
        MySqlDeviceRepository
        MySqlEventRepository
        NetbirdMappingRepository
    }

    AuthEndpoints --> AuthService
    AuthService --> JwtService
    AuthService --> Repositories

    UserEndpoints --> RoleCeiling
    UserEndpoints --> Repositories
    UserEndpoints --> AuditService

    CommandEndpoints --> ControlItFacade
    CommandEndpoints --> AuditService

    NetworkEndpoints --> TenantTargetResolver
    NetworkEndpoints --> TenantNetworkService
    NetworkEndpoints --> AuditService

    ControlItFacade --> Repositories
    ControlItFacade --> AuditService
    TenantNetworkService --> Repositories
```

## Security Invariants

| Area | Rule |
|---|---|
| Role ceiling | No actor manages equal-or-higher role. |
| Tenant scope | Tenant members stay inside own `tenant_id`. |
| Elevated network ops | SuperAdmin/CpAdmin provide `targetTenantId`. |
| Setup keys | Raw key returned once on create; list returns `[redacted]`. |
| Secrets | NetLock access keys and admin tokens stay backend-side. |
| Rate limits | Partitioned by authenticated actor, anonymous IP fallback. |
