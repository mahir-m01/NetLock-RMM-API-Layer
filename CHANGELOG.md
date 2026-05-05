# Changelog

## v0.1.0-alpha.1

Initial ControlIT alpha release package.

### Added

- ControlIT API and dashboard Docker Compose package.
- Bootstrap SuperAdmin setup flow.
- JWT auth with httpOnly refresh cookie sessions.
- Tenant-scoped device inventory and dashboard.
- SSE dashboard stream for live ControlIT events.
- Single-device and batch command execution through NetLock SignalR.
- Role ceiling and tenant-isolation guardrails.
- NetBird tenant group binding, setup-key flow, peer listing, and device linking.
- One-time NetBird setup key reveal with redacted list responses.
- ControlIT-owned audit, auth, refresh-token, password-reset, and NetBird mapping tables.
- Least-privilege runtime DB user setup.
- NetLock same-host Docker discovery helper.
- Host-level update and optional OTA scripts.

### Security Notes

- ControlIT does not install or modify NetLock.
- ControlIT writes only `controlit_*` tables.
- NetLock tokens and NetBird tokens must remain in `.env`.
- Bootstrap password must be changed after first login.

### Known Alpha Limits

- Compose update flow restarts API/web containers.
- NetLock and NetBird must be healthy before full dashboard/network validation.
- Non-standard NetLock deployments may require manual `.env` overrides.
