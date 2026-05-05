# ER 02 - ControlIT Owned Tables

```mermaid
erDiagram
    controlit_users {
        int id PK
        string email UK
        string password_hash
        int role
        int tenant_id
        string assigned_clients
        bool is_active
        bool must_change_password
        datetime password_changed_at
        int failed_login_count
        datetime locked_until
        datetime created_at
        datetime last_login_at
    }

    controlit_refresh_tokens {
        int id PK
        int user_id FK
        string token_hash UK
        datetime expires_at
        datetime revoked_at
        int replaced_by_id FK
        datetime created_at
        string user_agent
        string ip_address
    }

    controlit_password_reset_tokens {
        int id PK
        int user_id FK
        string token_hash UK
        datetime expires_at
        datetime used_at
        datetime created_at
    }

    controlit_audit_log {
        bigint id PK
        datetime timestamp
        int tenant_id
        string actor_key_id
        string actor_email
        string action
        string resource_type
        string resource_id
        string ip_address
        string result
        string error_message
    }

    controlit_device_netbird_map {
        int id PK
        int device_id UK
        string netbird_peer_id UK
        string netbird_ip
        string netbird_hostname
        datetime mapped_at
        string mapped_by
    }

    controlit_tenant_netbird_group {
        int id PK
        int tenant_id UK
        string netbird_group_id UK
        string netbird_group_name
        string isolation_policy_id
        string group_mode
        bool controlit_managed
        datetime created_at
        datetime updated_at
    }

    controlit_users ||--o{ controlit_refresh_tokens : issues
    controlit_users ||--o{ controlit_password_reset_tokens : requests
    controlit_refresh_tokens ||--o| controlit_refresh_tokens : replaced_by
```

## Important Fields

| Table | Notes |
|---|---|
| `controlit_users` | Bootstrap creates initial SuperAdmin from env when no user exists. |
| `controlit_refresh_tokens` | Raw token never stored; replay revokes user token chain. |
| `controlit_audit_log` | Actor email, action, result, tenant id retained for admin review. |
| `controlit_device_netbird_map` | Joins NetLock device id to NetBird peer id/IP. |
| `controlit_tenant_netbird_group` | Supports `managed`, `external`, `read_only` group modes. |
