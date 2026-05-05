# ControlIT - UML Diagrams

Diagrams describe current alpha app state. Markdown diagrams render in GitHub with Mermaid. PlantUML sources are kept for PNG regeneration, but this update intentionally does not touch PNG files.

## Use Case Diagrams

| Diagram | File | Description |
|---------|------|-------------|
| UC1 - Overall System | [uc1-overall.md](uc1-overall.md) | Actors and use cases across current alpha platform |
| UC2 - API Layer | [uc2-api-layer.md](uc2-api-layer.md) | REST API layer, JWT auth, tenant scoping, frontend-facing endpoints, external boundaries |
| UC2 - API Layer (PlantUML) | [uc2-api-layer.puml](uc2-api-layer.puml) | PlantUML source for same API-layer model |

## Class Diagrams

| Diagram | File | Description |
|---------|------|-------------|
| Class Diagram 01 - Runtime/Data Architecture | [class-01-netlockrmm.md](class-01-netlockrmm.md) | Current API runtime, repositories, NetLock/NetBird adapters, push hub, command dispatch |
| Class Diagram 02 - Auth/API Alpha | [class-02-auth.puml](class-02-auth.puml) | Current auth, JWT, RBAC, tenant target resolution, NetBird, dashboard push, command services |

## ER Diagrams

| Diagram | File | Description |
|---------|------|-------------|
| ER Diagram 01 - Data Boundary | [er-01-netlockrmm.md](er-01-netlockrmm.md) | NetLock read-only tables, ControlIT-owned `controlit_*` tables, external NetBird resources |
| ER Diagram 02 - Auth/API Alpha | [er-02-auth.puml](er-02-auth.puml) | Current ControlIT-owned tables plus read-only NetLock references |

## Sequence Diagrams

| Diagram | File | Description |
|---------|------|-------------|
| SEQ1 - Execute Command | [seq-01-execute-command.md](seq-01-execute-command.md) | JWT-authenticated single and batch command flow through tenant-scoped device lookup, NetLock SignalR, audit, and SSE status events |
| SEQ2 - Dashboard Push Stream | [seq-02-dashboard-push.md](seq-02-dashboard-push.md) | SSE dashboard stream, `NetLockLiveBridge`, `PushEventHub`, tenant filtering, degraded/offline states |
| SEQ3 - NetBird Tenant Onboarding | [seq-03-netbird-tenant-onboarding.md](seq-03-netbird-tenant-onboarding.md) | `targetTenantId` resolution, tenant group binding, setup-key reveal/redaction, peer enrollment/linking, NetBird API boundary |
| SEQ4 - Login | [seq-04-login.puml](seq-04-login.puml) | `POST /auth/login`: lockout, JWT issue, refresh cookie creation |
| SEQ5 - Refresh Rotation | [seq-05-refresh-rotation.puml](seq-05-refresh-rotation.puml) | `POST /auth/refresh`: hash lookup, rotation, replay revocation |
| SEQ6 - Password Change | [seq-06-password-change.puml](seq-06-password-change.puml) | `POST /auth/change-password`: current password check, policy, all-refresh-token revocation |
