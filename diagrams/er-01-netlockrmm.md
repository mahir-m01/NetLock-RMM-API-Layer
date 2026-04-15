# ER Diagram 01 — NetLock RMM + ControlIT Owned Tables

**Scope:** NetLock RMM tables read by ControlIT (read-only via Dapper) and ControlIT's own tables (owned via EF Core).
**Phase:** Phase 1 — Computer Port internal operations dashboard.
**Field names:** Verified against actual NetLock MySQL INSERT/UPDATE queries in `Authentification.cs`, `Event_Handler.cs`, and `Add_Tenant_Dialog.razor` / `Add_Location_Dialog.razor`.

---

```mermaid
erDiagram

    %% ─────────────────────────────────────────────────────────────────
    %% NETLOCKRMM TABLES — READ-ONLY
    %% ControlIT reads these via Dapper. Never writes. Never migrates.
    %% Column names verified against actual NetLock source INSERT queries.
    %% DO NOT use EF Core migrations on any of these tables.
    %% ─────────────────────────────────────────────────────────────────

    tenants {
        int     id                  PK
        string  guid
        string  name
        string  date
        string  author
        string  description
        string  company
        string  contact_person_one
        string  contact_person_two
        string  contact_person_three
        string  contact_person_four
        string  contact_person_five
    }

    locations {
        int     id          PK
        int     tenant_id   FK
        string  guid
        string  name
        string  date
        string  author
        string  description
    }

    devices {
        int     id                   PK
        int     tenant_id            FK
        string  tenant_name
        int     location_id          FK
        string  location_name
        string  device_name
        string  access_key
        string  hwid
        string  platform
        string  agent_version
        string  ip_address_internal
        string  ip_address_external
        string  operating_system
        string  domain
        string  antivirus_solution
        string  firewall_status
        string  architecture
        string  last_boot
        string  timezone
        string  cpu
        string  cpu_usage
        string  mainboard
        string  gpu
        string  ram
        string  ram_usage
        string  tpm
        string  last_active_user
        string  environment_variables
        string  last_access
        int     authorized
        int     synced
    }

    events {
        int     id                      PK
        int     device_id               FK
        string  tenant_name_snapshot
        string  location_name_snapshot
        string  device_name
        string  date
        string  severity
        string  reported_by
        string  _event
        string  description
        string  notification_json
        int     type
        string  language
    }

    %% ─────────────────────────────────────────────────────────────────
    %% CONTROLIT OWNED TABLES — EF Core (controlit_* prefix only)
    %% ControlIT creates and manages these via EF migrations.
    %% NetLock never touches these tables.
    %% ─────────────────────────────────────────────────────────────────

    controlit_device_map {
        int     id                  PK
        int     client_id           FK
        int     netlock_agent_id    FK
        string  netbird_peer_id
        string  wazuh_agent_id
    }

    controlit_audit_log {
        bigint  id              PK
        datetime logged_at
        int     tenant_id       FK
        string  actor_key_id
        string  action
        string  resource_type
        string  resource_id
        string  ip_address
        string  result
        string  error_message
    }

    controlit_tenant_api_keys {
        int      id          PK
        string   key_hash
        int      tenant_id   FK
        datetime created_at
        datetime expires_at
        datetime last_used_at
    }

    %% ─────────────────────────────────────────────────────────────────
    %% RELATIONSHIPS
    %% ─────────────────────────────────────────────────────────────────

    %% NetLock: one tenant → many locations (tenant_id FK on locations)
    tenants    ||--o{ locations          : "has many"

    %% NetLock: one tenant → many devices (tenant_id FK on devices)
    %% NOTE: devices also stores tenant_name and location_name as plain
    %% strings — these are denormalized copies written by the agent at
    %% registration time. Not kept in sync. Read as-is, never normalise.
    tenants    ||--o{ devices            : "has many"

    %% NetLock: one device → many events (device_id FK on events)
    devices    ||--o{ events             : "has many"

    %% NOTE: events stores tenant_name_snapshot as a plain string, NOT a FK.
    %% The name is captured at write time. If the tenant is later renamed,
    %% old events still show the original name. Do NOT join on this column.
    %% Shown here as a logical read reference only — no DB constraint exists.
    tenants    ||--o{ events             : "name snapshot only — not a FK"

    %% ControlIT: one device → zero or one device_map row (the unified link)
    %% netbird_peer_id and wazuh_agent_id are nullable — Phase 1 leaves them
    %% empty. A device exists in NetLock before it is enrolled in Netbird/Wazuh.
    devices    ||--o| controlit_device_map  : "mapped in zero or one"
    tenants    ||--o| controlit_device_map  : "owned by client"

    %% ControlIT: audit log is mandatory — every action must be recorded.
    %% Required for DPDP Act 2023 compliance before enterprise onboarding.
    tenants    ||--o{ controlit_audit_log   : "has many"

    %% ControlIT: Seed one row here before first run — see Contract 01 "First Run Setup".
    %% This table is created now to avoid a schema gap when Phase 2
    %% per-tenant key enforcement ships.
    tenants    ||--o{ controlit_tenant_api_keys    : "has many"
```

