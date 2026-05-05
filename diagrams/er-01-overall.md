# ER 01 - Overall Data Boundary

```mermaid
erDiagram
    NETLOCK_MYSQL ||--o{ NETLOCK_TENANTS : owns
    NETLOCK_MYSQL ||--o{ NETLOCK_DEVICES : owns
    NETLOCK_MYSQL ||--o{ NETLOCK_EVENTS : owns
    NETLOCK_MYSQL ||--o{ NETLOCK_ACCOUNTS : owns

    CONTROLIT_DB ||--o{ CONTROLIT_USERS : owns
    CONTROLIT_DB ||--o{ CONTROLIT_REFRESH_TOKENS : owns
    CONTROLIT_DB ||--o{ CONTROLIT_AUDIT_LOG : owns
    CONTROLIT_DB ||--o{ CONTROLIT_DEVICE_NETBIRD_MAP : owns
    CONTROLIT_DB ||--o{ CONTROLIT_TENANT_NETBIRD_GROUP : owns

    NETBIRD_API ||--o{ NETBIRD_GROUPS : external
    NETBIRD_API ||--o{ NETBIRD_PEERS : external
    NETBIRD_API ||--o{ NETBIRD_SETUP_KEYS : external

    NETLOCK_TENANTS ||--o{ NETLOCK_DEVICES : tenant_id
    NETLOCK_DEVICES ||--o{ NETLOCK_EVENTS : device_id
    NETLOCK_DEVICES ||--o| CONTROLIT_DEVICE_NETBIRD_MAP : device_id
    NETLOCK_TENANTS ||--o| CONTROLIT_TENANT_NETBIRD_GROUP : tenant_id
    CONTROLIT_TENANT_NETBIRD_GROUP ||--|| NETBIRD_GROUPS : netbird_group_id
    CONTROLIT_DEVICE_NETBIRD_MAP ||--|| NETBIRD_PEERS : netbird_peer_id
```

## Ownership

| Boundary | ControlIT behavior |
|---|---|
| NetLock MySQL | Dapper read-only. No migrations. No writes. |
| ControlIT tables | EF Core creates/migrates only `controlit_*`. |
| NetBird API | HTTP adapter. ControlIT stores references and mapping only. |
