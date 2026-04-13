# ControlIT — UML Diagrams

All diagrams use Mermaid and render natively on GitHub.

## Use Case Diagrams

| Diagram | File | Description |
|---------|------|-------------|
| UC1 — Overall System | [uc1-overall.md](uc1-overall.md) | Actors and use cases across the full platform |
| UC2 — API Layer | [uc2-api-layer.md](uc2-api-layer.md) | REST API endpoints, middleware, and integrations |

## Class Diagrams

| Diagram | File | Description |
|---------|------|-------------|
| Class Diagram 01 — NetLock RMM | [class-01-netlockrmm.md](class-01-netlockrmm.md) | OOP structure, interfaces, design patterns (Phase 1) |

## ER Diagrams

| Diagram | File | Description |
|---------|------|-------------|
| ER Diagram 01 — NetLock RMM | [er-01-netlockrmm.md](er-01-netlockrmm.md) | Database schema — NetLock tables + ControlIT owned tables |

## Sequence Diagrams

| Diagram | File | Description |
|---------|------|-------------|
| SEQ1 — Execute Command | [seq-01-execute-command.md](seq-01-execute-command.md) | Full flow for `POST /commands/execute`: API key validation, tenant resolution, SignalR dispatch, responseId correlation, audit logging, timeout and disconnect error paths |
