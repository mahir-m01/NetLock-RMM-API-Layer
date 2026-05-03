# ControlIT Alpha Release

ControlIT is a business-facing control layer for NetLock RMM with NetBird network visibility. It gives operators one dashboard for device status, live updates, command execution, tenant setup keys, and NetBird peer mapping.

This `production` branch is for alpha testers and deployment validation only. Architecture diagrams, university project material, and deep API documentation live on `main` / `dev`.

## What This Release Includes

- ControlIT API and dashboard.
- NetLock integration without editing NetLock source.
- NetBird Cloud or self-hosted NetBird integration.
- Generated bootstrap admin credentials during setup.
- One-time ControlIT database migrations.
- Least-privilege runtime database user.
- Push dashboard updates through SSE.
- Tenant-scoped NetBird setup key and peer mapping flow.

## Requirements

- Docker + Docker Compose.
- MySQL/NetLock stack from this repo or existing compatible NetLock deployment.
- NetBird account or self-hosted NetBird management server.
- NetBird personal access token with management API access.
- Linux x86_64 host recommended for real demos.

macOS local demo:

```bash
brew install colima docker docker-compose
colima start --arch aarch64 --vm-type vz --vz-rosetta --cpu 4 --memory 6
```

## Fresh Install

1. Clone production branch:

```bash
git clone -b production https://github.com/mahir-m01/NetLock-RMM-API-Layer.git
cd NetLock-RMM-API-Layer
```

2. Generate environment and bootstrap credentials:

```bash
./scripts/setup-controlit-env.sh
```

This creates `.env`, generates passwords/signing keys, and prints initial SuperAdmin login once. Save it securely, then change password after first login.

3. Fill required `.env` values:

```bash
CONTROLIT_NETLOCK_TOKEN=<remote_session_token from NetLock accounts table>
CONTROLIT_NETLOCK_FILES_KEY=<NetLock files_api_key>
NETBIRD_BASE_URL=https://api.netbird.io
NETBIRD_TOKEN=<NetBird personal access token>
```

For self-hosted NetBird, replace `NETBIRD_BASE_URL` with your management API URL.

4. Start NetLock and MySQL:

```bash
docker compose up -d
```

Wait until MySQL and NetLock are healthy.

5. Run ControlIT migrations once:

```bash
./scripts/run-controlit-migrations.sh
```

6. Create least-privilege runtime DB user:

```bash
./scripts/apply-controlit-db-user.sh
```

7. Start ControlIT API and dashboard:

```bash
docker compose -f docker-compose.controlit.yml up -d --build
```

8. Verify:

```bash
curl -f http://localhost:5290/health/ready
curl -f http://localhost:3000
```

Open dashboard:

```text
http://localhost:3000
```

Login using bootstrap SuperAdmin credentials printed by setup script.

## Existing NetBird Customer Setup

Use this when customer already has NetBird groups and devices.

1. Add NetBird API values to `.env`.
2. Login as SuperAdmin or CpAdmin.
3. Open Network page.
4. Pick tenant.
5. Bind existing NetBird group in external/read-only mode.

Modes:

| Mode | Use |
|---|---|
| `external` | Customer-owned group. ControlIT reads and maps peers, but does not manage ownership. |
| `read_only` | Visibility-only demo. No ControlIT changes to group/policy. |
| `managed` | ControlIT-created tenant group/policy for new deployments. |

## New NetBird Tenant Setup

Use this when ControlIT should create the tenant network path.

1. Login as elevated admin.
2. Open Network page.
3. Select tenant.
4. Create setup key.
5. Copy raw key immediately. It is shown once only.
6. Install NetBird agent on device with that key.
7. Link NetBird peer to NetLock device from Network page.

## Device Install Pattern

Install both agents per device:

1. NetLock agent from NetLock console tenant installer.
2. NetBird agent using tenant setup key.

For internal/agent testing, use the included Debian Lima VM instead of enrolling personal laptops or random machines:

```bash
brew install lima
limactl start debian-test.yaml
limactl shell debian-test
```

Inside the VM, run the NetLock tenant installer from the NetLock console, then run the NetBird setup-key command for the same tenant. This gives a clean disposable test endpoint for dashboard, command, and NetBird peer validation.

Linux NetBird example:

```bash
curl -fsSL https://pkgs.netbird.io/install.sh | sh
sudo netbird up --management-url "<netbird_management_url>" --setup-key "<tenant_setup_key>"
```

NetLock install command must come from NetLock web console for correct tenant/server values.

## Demo Checklist

- Dashboard loads after login.
- Stream state shows connected.
- Debian or customer test device appears online without page refresh.
- Devices page shows NetBird IP when peer linked.
- Recent Devices shows same NetBird IP state as Devices page.
- Network page shows peers/setup keys for selected tenant.
- Setup key list never shows raw key after creation.
- System health explains degraded NetLock/NetBird parts.
- No secrets appear in UI or logs.

## Security Notes

- Never commit `.env`.
- Rotate demo NetBird token/setup keys before real external use.
- Change bootstrap password immediately.
- Keep `CONTROLIT_AUTO_MIGRATE=false` for demo/production runtime.
- Run migrations only through `scripts/run-controlit-migrations.sh`.
- Runtime API must use `CONTROLIT_DB_USER`, not MySQL root.
- ControlIT must not edit NetLock source.
- ControlIT writes only `controlit_*` tables.

## Useful Commands

```bash
docker compose ps
docker compose -f docker-compose.controlit.yml ps
docker logs controlit-api --tail=100
docker logs controlit-web --tail=100
```

Restart ControlIT:

```bash
docker compose -f docker-compose.controlit.yml up -d --build
```

Stop all local services:

```bash
docker compose -f docker-compose.controlit.yml down
docker compose down
```

## Full Release Notes

See [RELEASE.md](RELEASE.md) for detailed rotation notes, NetBird API examples, and environment variable reference.
