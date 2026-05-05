<p align="center">
  <img src="src/ControlIT.Web/public/logo.png" alt="ControlIT logo" width="96" height="96">
</p>

<h1 align="center">ControlIT</h1>

<p align="center">
  NetLock RMM API layer and operations dashboard by <strong>Computer Port</strong>.
</p>

<p align="center">
  <a href="https://github.com/mahir-m01/NetLock-RMM-API-Layer/releases/latest">Latest release</a>
  ·
  <a href="PACKAGE.md">Release package</a>
  ·
  <a href="RELEASE.md">Operations guide</a>
  ·
  <a href="CHANGELOG.md">Changelog</a>
</p>

---

## About

ControlIT is an alpha operations layer for existing NetLock RMM deployments. It adds a focused web dashboard, tenant-aware access control, live device status, command execution, batch command dispatch, NetBird network visibility, setup-key management, audit logging, and system health checks.

ControlIT sits beside NetLock. NetLock remains the endpoint-management system of record. ControlIT does not install NetLock, does not modify NetLock source, and does not write to NetLock-owned tables. ControlIT writes only its own `controlit_*` tables.

## Alpha Release

| Item | Status |
|---|---|
| Release channel | `production` branch |
| Current version | `v0.1.0-alpha.1` |
| Package type | Docker Compose source package |
| Runtime | `controlit-api` + `controlit-web` |
| Dependency | Existing healthy NetLock RMM installation |
| Network integration | NetBird Cloud or self-hosted NetBird Management API |
| Update model | Host-level OTA scripts for the Compose deployment |

This release is suitable for controlled alpha demos and internal validation. It is not yet positioned as a final production-ready RMM platform.

## Core Features

- Operator dashboard for endpoint and tenant operations.
- Live dashboard updates through server-sent events.
- Device inventory sourced from NetLock.
- Online/offline state sourced from NetLock live connection state.
- Single-device and batch command execution through NetLock SignalR.
- Tenant-scoped users, roles, devices, setup keys, and NetBird mappings.
- NetBird Cloud or self-hosted NetBird Management API support.
- Existing NetBird group binding for customer-owned networks.
- ControlIT-managed NetBird tenant group/setup-key flow for new deployments.
- One-time setup-key reveal. Key lists stay redacted.
- Audit log for administrative actions.
- Health endpoints for API, MySQL, NetLock SignalR, and NetBird.
- Least-privilege runtime database user.

## Deployment Requirements

- Docker and Docker Compose.
- Existing NetLock RMM deployment already installed and healthy.
- Standard NetLock Docker install on the same host, or manual NetLock connection values for non-standard installs.
- NetBird Cloud account or self-hosted NetBird Management server.
- NetBird personal access token with management API access.
- DNS/TLS/reverse proxy configuration when exposing ControlIT outside a private network.

ControlIT setup expects NetLock to be installed, configured, healthy, and reachable before ControlIT starts. On standard same-host Docker installs, the installer discovers NetLock values automatically. Operators should not need to query NetLock MySQL manually.

## Quick Install

Clone the release branch beside the existing NetLock installation:

```bash
cd /opt
git clone -b production https://github.com/mahir-m01/NetLock-RMM-API-Layer.git controlit
cd controlit
```

Generate environment and bootstrap credentials:

```bash
./scripts/setup-controlit-env.sh
```

Set browser-facing and NetBird values in `.env`:

```bash
CONTROLIT_PUBLIC_API_URL=https://api.<your-domain>
CONTROLIT_ALLOWED_ORIGINS=https://app.<your-domain>
NETBIRD_BASE_URL=https://api.netbird.io
NETBIRD_TOKEN=<NetBird personal access token>
```

Install ControlIT:

```bash
./scripts/install-controlit.sh
```

The installer applies ControlIT migrations, creates the least-privilege runtime DB user, starts API/web containers, and waits for `/health/ready`.

Open the dashboard at the configured web origin and login with the bootstrap SuperAdmin credentials printed by `setup-controlit-env.sh`. Change the bootstrap password after first login.

## Standard NetLock Discovery

For standard same-host NetLock Docker installs, ControlIT discovers:

- NetLock MySQL root password from the existing NetLock `.env`.
- NetLock database and MySQL container.
- NetLock Docker network.
- NetLock SignalR hub URL.
- NetLock `remote_session_token`.
- NetLock `files_api_key`.

For custom installs:

```bash
CONTROLIT_NETLOCK_ENV_FILE=/path/to/netlock/.env ./scripts/install-controlit.sh
```

or edit the NetLock values directly in `.env`.

## NetBird Setup

Use existing NetBird network:

1. Set `NETBIRD_BASE_URL` and `NETBIRD_TOKEN`.
2. Login as SuperAdmin or CpAdmin.
3. Open Network.
4. Select tenant.
5. Bind tenant to an existing NetBird group using `external` or `read_only` mode.

Create ControlIT-managed NetBird path:

1. Login as SuperAdmin or CpAdmin.
2. Open Network.
3. Select tenant.
4. Create setup key.
5. Copy raw key immediately. It is shown once only.
6. Install NetBird agent on endpoint with that tenant key.
7. Link NetBird peer to NetLock device in ControlIT.

| Mode | Purpose |
|---|---|
| `external` | Customer-owned NetBird group. ControlIT reads and maps peers. |
| `read_only` | Visibility-only NetBird integration. |
| `managed` | ControlIT-created tenant group/policy/setup-key flow. |

## Endpoint Enrollment

Each managed endpoint needs both agents:

1. NetLock agent from tenant installer generated by NetLock.
2. NetBird agent enrolled with tenant setup key.

Linux NetBird enrollment:

```bash
curl -fsSL https://pkgs.netbird.io/install.sh | sh
sudo netbird up --management-url "<netbird_management_url>" --setup-key "<tenant_setup_key>"
```

NetLock installer command must come from the NetLock console so tenant and server values match the NetLock deployment.

## Updates

Current update model: host-level OTA for the ControlIT Compose deployment. API and web containers restart during update. Database data remains in existing NetLock MySQL. ControlIT schema changes are applied through EF migrations before runtime containers are replaced.

Check for update:

```bash
./scripts/check-controlit-update.sh
```

Update:

```bash
./scripts/update-controlit.sh
```

Enable scheduled OTA updates on Linux/systemd hosts:

```bash
./scripts/install-controlit-ota.sh
```

Rollback:

```bash
git log --oneline -5
git checkout <previous-production-commit>
docker compose up -d --build
curl -f http://localhost:5290/health/ready
```

## Operations

```bash
docker compose ps
docker logs controlit-api --tail=100
docker logs controlit-web --tail=100
curl -f http://localhost:5290/health/live
curl -f http://localhost:5290/health/ready
```

Restart ControlIT:

```bash
docker compose up -d --build
```

Stop ControlIT:

```bash
docker compose down
```

## Security Boundary

- Keep `.env` private.
- Rotate bootstrap password after first login.
- Rotate NetBird tokens and setup keys after operational handoff.
- Keep `CONTROLIT_AUTO_MIGRATE=false` during runtime.
- Run migrations only through `scripts/run-controlit-migrations.sh`.
- Run API with `CONTROLIT_DB_USER`, not MySQL root.
- ControlIT must not edit NetLock source.
- ControlIT writes only `controlit_*` tables.
- NetLock bootstrap/vendor configuration files are external prerequisites, not ControlIT-owned secrets.

## More

- [Release package](PACKAGE.md)
- [Release guide](RELEASE.md)
- [Changelog](CHANGELOG.md)
