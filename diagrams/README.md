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
| UC1 - Overall Platform | [use-case/uc1-overall.puml](use-case/uc1-overall.puml) | [use-case/uc1-overall.png](use-case/uc1-overall.png) | README/showcase diagram: actors, main product capabilities, external systems |
| UC2 - Roles and Access | [use-case/uc2-roles-and-access.puml](use-case/uc2-roles-and-access.puml) | [use-case/uc2-roles-and-access.png](use-case/uc2-roles-and-access.png) | RBAC, tenant isolation, role ceiling, elevated tenant targeting |
| UC3 - NetBird Operations | [use-case/uc3-netbird-operations.puml](use-case/uc3-netbird-operations.puml) | [use-case/uc3-netbird-operations.png](use-case/uc3-netbird-operations.png) | NetBird group binding, setup keys, peer enrollment/linking |

## Class Diagrams

| Diagram | File | Purpose |
|---|---|---|
| Class 01 - Overall Architecture | [class/class-01-overall.md](class/class-01-overall.md) | High-level ControlIT modules and external boundaries |
| Class 02 - API Services | [class/class-02-api-services.md](class/class-02-api-services.md) | Auth, RBAC, tenant resolution, repositories, command services |
| Class 03 - Integrations | [class/class-03-integrations.md](class/class-03-integrations.md) | NetLock, NetBird, push hub, live bridge adapters |

## ER Diagrams

| Diagram | File | Purpose |
|---|---|---|
| ER 01 - Overall Data Boundary | [er/er-01-overall.md](er/er-01-overall.md) | High-level data ownership map |
| ER 02 - ControlIT Owned Tables | [er/er-02-controlit-owned.md](er/er-02-controlit-owned.md) | EF Core `controlit_*` schema |
| ER 03 - External Read/API Boundary | [er/er-03-external-boundary.md](er/er-03-external-boundary.md) | NetLock read-only tables and NetBird external resources |

## Sequence Diagrams

| Diagram | File | Purpose |
|---|---|---|
| SEQ0 - MVP Runtime Flow | [sequence/seq-00-mvp-runtime.md](sequence/seq-00-mvp-runtime.md) | End-to-end dashboard/device/network flow |
| SEQ1 - Command Execution | [sequence/seq-01-command-execution.md](sequence/seq-01-command-execution.md) | Single and batch commands through NetLock SignalR |
| SEQ2 - Dashboard Push | [sequence/seq-02-dashboard-push.md](sequence/seq-02-dashboard-push.md) | SSE stream, push hub, live bridge, no polling fallback |
| SEQ3 - NetBird Onboarding | [sequence/seq-03-netbird-onboarding.md](sequence/seq-03-netbird-onboarding.md) | Tenant group, setup key, peer enrollment/link |
| SEQ4 - Auth Session | [sequence/seq-04-auth-session.md](sequence/seq-04-auth-session.md) | Login, refresh rotation, password change/reset |
