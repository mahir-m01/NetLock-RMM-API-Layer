# ER 03 - External Read/API Boundary

```mermaid
erDiagram
    tenants {
        int id PK
        string guid
        string name
        string company
    }

    locations {
        int id PK
        int tenant_id FK
        string name
    }

    devices {
        int id PK
        int tenant_id FK
        int location_id FK
        string device_name
        string access_key
        string platform
        string operating_system
        string agent_version
        string ip_address_internal
        string ip_address_external
        datetime last_access
        int authorized
        int synced
    }

    events {
        int id PK
        int device_id FK
        string tenant_name_snapshot
        string location_name_snapshot
        string device_name
        datetime date
        string severity
        string reported_by
        string event_name
        string description
        int type
    }

    accounts {
        int id PK
        string remote_session_token
    }

    netbird_groups {
        string id PK
        string name
    }

    netbird_peers {
        string id PK
        string hostname
        string ip
        bool connected
        datetime last_seen
    }

    netbird_setup_keys {
        string id PK
        string key_secret
        string name
        string type
        bool revoked
        int used_times
        int usage_limit
    }

    tenants ||--o{ locations : has
    tenants ||--o{ devices : has
    locations ||--o{ devices : hosts
    devices ||--o{ events : emits
    netbird_groups ||--o{ netbird_peers : contains
    netbird_groups ||--o{ netbird_setup_keys : auto_groups
```

## Boundary Rules

- `devices.access_key` and `accounts.remote_session_token` are backend secrets.
- Online state comes from NetLock live connection snapshot, not `last_access`.
- `events.tenant_name_snapshot` is a name snapshot, not tenant FK.
- NetBird setup key secret is revealed once by create response, then redacted in list responses.
