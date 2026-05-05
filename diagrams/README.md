# ControlIT UML Diagrams

Current alpha architecture diagrams.

Rules:
- Use case diagrams use PlantUML (`.puml`) plus generated PNGs because Mermaid does not support use case diagrams well.
- Class, ER, and sequence diagrams use Mermaid inside Markdown so they render on GitHub.
- NetLock is a pre-existing dependency. ControlIT reads/calls NetLock; it does not edit NetLock source or own NetLock tables.
- NetBird is represented as an external Management API. No NetBird source/database ownership is implied.

## Use Case Diagrams

| Diagram | Source | PNG | Purpose |
|---|---|---|---|
| UC1 - Overall Platform | [uc1-overall.puml](uc1-overall.puml) | [uc1-overall.png](uc1-overall.png) | README/showcase diagram: actors, main product capabilities, external systems |
| UC2 - Roles and Access | [uc2-roles-and-access.puml](uc2-roles-and-access.puml) | [uc2-roles-and-access.png](uc2-roles-and-access.png) | RBAC, tenant isolation, role ceiling, elevated tenant targeting |
| UC3 - NetBird Operations | [uc3-netbird-operations.puml](uc3-netbird-operations.puml) | [uc3-netbird-operations.png](uc3-netbird-operations.png) | NetBird group binding, setup keys, peer enrollment/linking |

## Class Diagrams

| Diagram | File | Purpose |
|---|---|---|
| Class 01 - Overall Architecture | [class-01-overall.md](class-01-overall.md) | High-level ControlIT modules and external boundaries |
| Class 02 - API Services | [class-02-api-services.md](class-02-api-services.md) | Auth, RBAC, tenant resolution, repositories, command services |
| Class 03 - Integrations | [class-03-integrations.md](class-03-integrations.md) | NetLock, NetBird, push hub, live bridge adapters |

## ER Diagrams

| Diagram | File | Purpose |
|---|---|---|
| ER 01 - Overall Data Boundary | [er-01-overall.md](er-01-overall.md) | High-level data ownership map |
| ER 02 - ControlIT Owned Tables | [er-02-controlit-owned.md](er-02-controlit-owned.md) | EF Core `controlit_*` schema |
| ER 03 - External Read/API Boundary | [er-03-external-boundary.md](er-03-external-boundary.md) | NetLock read-only tables and NetBird external resources |

## Sequence Diagrams

| Diagram | File | Purpose |
|---|---|---|
| SEQ0 - MVP Runtime Flow | [seq-00-mvp-runtime.md](seq-00-mvp-runtime.md) | End-to-end dashboard/device/network flow |
| SEQ1 - Command Execution | [seq-01-command-execution.md](seq-01-command-execution.md) | Single and batch commands through NetLock SignalR |
| SEQ2 - Dashboard Push | [seq-02-dashboard-push.md](seq-02-dashboard-push.md) | SSE stream, push hub, live bridge, no polling fallback |
| SEQ3 - NetBird Onboarding | [seq-03-netbird-onboarding.md](seq-03-netbird-onboarding.md) | Tenant group, setup key, peer enrollment/link |
| SEQ4 - Auth Session | [seq-04-auth-session.md](seq-04-auth-session.md) | Login, refresh rotation, password change/reset |
