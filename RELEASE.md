# ControlIT Release Guide

This branch contains the deployable ControlIT release package: API, dashboard, Docker files, setup scripts, migrations, environment template, and operational documentation.

NetLock remains vendor-owned. ControlIT reads NetLock data, bridges NetLock live state into ControlIT push events, dispatches commands through NetLock SignalR, and writes only ControlIT-owned `controlit_*` tables.

## Included Components

- `controlit-api`: ASP.NET Core API.
- `controlit-web`: Next.js dashboard.
- `docker-compose.yml`: ControlIT-only Compose deployment.
- `scripts/setup-controlit-env.sh`: generates `.env` and bootstrap credentials.
- `scripts/install-controlit.sh`: applies migrations, creates DB user, starts services, checks readiness.
- `scripts/run-controlit-migrations.sh`: applies ControlIT EF migrations.
- `scripts/apply-controlit-db-user.sh`: creates least-privilege runtime DB grants.
- `.env.example`: required environment reference.

## Required Environment

| Variable | Purpose |
|---|---|
| `MYSQL_ROOT_PASSWORD` | Existing NetLock MySQL root password or migration user password. |
| `MYSQL_DATABASE` | Existing NetLock database, default `iphbmh`. |
| `MYSQL_CONTAINER` | Existing NetLock MySQL container name for local Docker deployments. |
| `CONTROLIT_DB_HOST` | Existing NetLock MySQL host reachable from ControlIT containers. |
| `CONTROLIT_DB_PORT` | Existing NetLock MySQL port. |
| `NETLOCK_DOCKER_NETWORK` | Existing Docker network shared with NetLock. |
| `CONTROLIT_DB_USER` | Dedicated ControlIT runtime DB user. |
| `CONTROLIT_DB_PASSWORD` | Dedicated ControlIT runtime DB password. |
| `CONTROLIT_JWT_SIGNING_KEY` | HS256 signing key, at least 32 bytes. |
| `CONTROLIT_BOOTSTRAP_EMAIL` | Initial SuperAdmin email. |
| `CONTROLIT_BOOTSTRAP_PASSWORD` | Initial SuperAdmin password. |
| `CONTROLIT_NETLOCK_TOKEN` | NetLock `remote_session_token` for SignalR command hub. |
| `CONTROLIT_NETLOCK_FILES_KEY` | NetLock `files_api_key` for admin status endpoints. |
| `CONTROLIT_NETLOCK_HUB_URL` | NetLock SignalR command hub URL. |
| `CONTROLIT_PUBLIC_API_URL` | Browser-facing ControlIT API URL baked into web build. |
| `CONTROLIT_ALLOWED_ORIGINS` | Comma-separated browser origins allowed by ControlIT API CORS. |
| `NETBIRD_BASE_URL` | NetBird Management API URL. |
| `NETBIRD_TOKEN` | NetBird personal access token. |
| `CONTROLIT_AUTO_MIGRATE` | Keep `false` during runtime. |

## Install Flow

1. Generate `.env`:

```bash
./scripts/setup-controlit-env.sh
```

2. Complete required NetLock and NetBird values in `.env`.

3. Install:

```bash
./scripts/install-controlit.sh
```

## Update Flow

ControlIT updates are designed as controlled maintenance updates. Existing data remains in MySQL. Containers are rebuilt and replaced. API/web downtime equals container restart time plus migration time.

Recommended sequence:

```bash
./scripts/update-controlit.sh
```

The script creates an `.env` backup, fast-forwards the release branch, applies ControlIT migrations, refreshes runtime grants, replaces containers, and waits for `/health/ready`.

Rollback sequence:

```bash
git log --oneline -5
git checkout <previous-production-commit>
docker compose up -d --build
curl -f http://localhost:5290/health/ready
```

Migration policy:

- Prefer additive schema changes.
- Preserve old columns until all running code no longer depends on them.
- Back up MySQL before destructive migrations.
- Keep `CONTROLIT_AUTO_MIGRATE=false` during normal runtime.
- Run migrations as an explicit operator step.

Zero-downtime update is not the current Compose path. For that, deploy versioned images behind a reverse proxy or orchestrator, run migration preflight, start new containers, wait for `/health/ready`, then switch traffic.

## NetLock Boundary

- ControlIT does not modify NetLock source.
- ControlIT does not write NetLock-owned tables.
- EF Core models only ControlIT-owned `controlit_*` tables.
- Dapper repositories read NetLock-owned tables.
- NetLock SignalR remains the command transport.
- NetLock live connection state remains the online/offline source.

## NetBird Operating Modes

| Mode | Ownership | Behavior |
|---|---|---|
| `external` | Customer-owned | ControlIT maps peers and reads group state. |
| `read_only` | Customer-owned | ControlIT displays network state only. |
| `managed` | ControlIT-managed | ControlIT creates tenant group/policy/setup keys. |

Setup keys are reusable secrets. ControlIT reveals a raw setup key only once at creation and redacts keys in list views.

## Credential Rotation

- Bootstrap password: change after first login.
- JWT signing key: rotate `CONTROLIT_JWT_SIGNING_KEY`, restart API, active sessions become invalid.
- NetLock SignalR token: update `CONTROLIT_NETLOCK_TOKEN`, restart API.
- NetLock files API key: update `CONTROLIT_NETLOCK_FILES_KEY`, restart API.
- NetBird PAT: rotate in NetBird, update `NETBIRD_TOKEN`, restart API.
- NetBird setup keys: delete old keys after endpoint enrollment.
- Runtime DB password: change MySQL user password, update `.env`, restart API.

## Operational Checks

- `/health/live`: process liveness.
- `/health/ready`: MySQL and NetLock SignalR readiness.
- `/health`: component health, including NetBird degradation.
- `/admin/system-health`: authenticated administrative health view.

Useful commands:

```bash
docker compose ps
docker logs controlit-api --tail=100
docker logs controlit-web --tail=100
```
