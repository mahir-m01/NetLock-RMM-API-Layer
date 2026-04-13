# ControlIT

A unified endpoint management API layer built on top of NetLock RMM, Netbird, and Wazuh. Built for managed service providers who need a single dashboard across multiple client environments — instead of logging into three separate tools per client.

This repo is the integration layer. It reads from NetLock's database, dispatches commands through its SignalR hub, and serves everything through a clean REST API to a Next.js dashboard.

---

## What it does

- Lists and details all managed endpoints across all client tenants
- Dispatches remote commands to endpoints in real time via NetLock's SignalR hub
- Tracks every action in a mandatory audit log (DPDP Act 2023 compliance)
- Maps each physical device across NetLock, Netbird, and Wazuh into one unified identity
- Enforces tenant isolation server-side — every query is scoped to the requesting tenant

Phase 1 covers NetLock RMM and Netbird. Wazuh SIEM integration is Phase 2.

---

## System Overview

![UC1 — Control IT Overall Use Case Diagram](diagrams/uc1-overall.png)

---

## Stack

| Layer | Technology |
|---|---|
| API runtime | ASP.NET Core 10 — Minimal APIs |
| NetLock reads | Dapper + MySqlConnector |
| ControlIT tables | EF Core + Pomelo |
| Real-time commands | Microsoft.AspNetCore.SignalR.Client |
| Dashboard | Next.js (TypeScript, App Router) |
| Database | MySQL 8.0 (shared with NetLock) |
| Reverse proxy | Caddy |
| Containerisation | Docker Compose |

---

## Diagrams

All diagrams are in the [`/diagrams`](diagrams/) folder and render on GitHub.

| Diagram | Description |
|---|---|
| [UC1 — Overall System](diagrams/uc1-overall.md) | All actors and use cases across the full platform |
| [UC2 — API Layer](diagrams/uc2-api-layer.md) | REST endpoints, middleware, and external integrations |
| [Class Diagram — NetLock RMM](diagrams/class-01-netlockrmm.md) | OOP structure, interfaces, and design patterns |
| [ER Diagram — NetLock RMM](diagrams/er-01-netlockrmm.md) | Database schema — NetLock tables and ControlIT owned tables |
| [SEQ1 — Execute Command](diagrams/seq-01-execute-command.md) | Full flow for `POST /commands/execute` — API key validation, SignalR dispatch, responseId correlation, audit logging, timeout and disconnect error paths |

---

## Local Development

This repo includes the local Docker stack for running NetLock RMM on Apple Silicon (M-series) via Colima.

### Containers

| Container | Image | Port | Purpose |
|---|---|---|---|
| `mysql-container` | `mysql:8.0` | `3306` | Database (native arm64) |
| `netlock-rmm-server` | `nicomak101/netlock-rmm-server` | `7080` / `7082` | Backend — all server roles |
| `netlock-rmm-web-console` | `nicomak101/netlock-rmm-web-console` | `8080` | Blazor admin web UI |

> The two NetLock images are amd64-only and run under Rosetta 2 emulation via Colima's `--vz-rosetta` flag. MySQL runs natively on arm64.
>
> The relay is on host port `7082` (not `7081`) because macOS SSH port forwarding holds `7081` on this machine.

### Prerequisites

```bash
brew install colima docker docker-compose
```

Wire the Compose plugin to Docker CLI (one-time):

```bash
mkdir -p ~/.docker/cli-plugins
ln -sfn /opt/homebrew/opt/docker-compose/bin/docker-compose ~/.docker/cli-plugins/docker-compose
```

### 1. Start Colima

Run after every Mac restart — Colima does not auto-start:

```bash
colima start --arch aarch64 --vm-type vz --vz-rosetta --cpu 4 --memory 6
```

> Use `--vz-rosetta`, not `--rosetta`. The short flag does not exist in this version.

| Flag | Why |
|---|---|
| `--arch aarch64` | Native ARM64 VM |
| `--vm-type vz` | Apple Virtualization Framework — faster than QEMU |
| `--vz-rosetta` | Rosetta 2 inside the VM for amd64 containers |
| `--cpu 4` | Adjust to taste |
| `--memory 6` | 6 GB needed for MySQL + two .NET containers |

### 2. Configure your API key

```bash
cp .env.example .env
```

Edit `.env` and set `NETLOCK_API_KEY` from your NetLock members portal. The `appsettings.json` files in `config/` contain only structural defaults — all secrets come from `.env` at runtime.

### 3. Start the stack

```bash
docker compose up -d
```

First run pulls around 350 MB. MySQL runs a healthcheck before the NetLock containers start — allow 2–3 minutes on first boot.

### Access

| Service | URL | Credentials |
|---|---|---|
| NetLock Web Console | http://localhost:8080 | `admin` / `admin` |
| NetLock Server API | http://localhost:7080 | — |
| MySQL | `localhost:3306` | root / see `.env` |

### Useful commands

```bash
docker compose ps                          # check statuses
docker compose logs -f                     # stream all logs
docker compose logs -f netlock-rmm-server  # one container
docker compose down                        # stop, preserve data
docker compose pull && docker compose up -d  # pull latest

# Full reset — wipes all data
docker compose down -v && rm -rf data/mysql data/server data/web_console && docker compose up -d
```

---

## Test Agent — Debian Lima VM

A Debian 12 (ARM64) Lima VM is included for testing the NetLock Linux agent locally.

```bash
brew install lima
limactl start debian-test.yaml
limactl shell debian-test
```

Inside the VM, download the installer from the NetLock web console at `http://localhost:8080`. Set all server fields to `192.168.5.2:7080` and relay to `192.168.5.2:7082`, then run the installer. The agent will appear in the console under your selected tenant.

> `192.168.5.2` is Lima's fixed host gateway — always resolves to your Mac from inside any Lima VM.

```bash
limactl stop debian-test
limactl delete debian-test  # wipes disk
```

---

## Folder Structure

```
NetLock-RMM-API-Layer/
├── docker-compose.yml
├── debian-test.yaml            # Lima VM config for local agent testing
├── .env                        # Secrets — gitignored, never commit
├── .env.example                # Template — safe to commit
├── config/
│   ├── server/appsettings.json
│   └── web_console/appsettings.json
├── data/                       # Gitignored — written by containers at runtime
└── diagrams/                   # UML diagrams — render on GitHub
```

---

## Troubleshooting

**`docker compose` not found**
```bash
mkdir -p ~/.docker/cli-plugins
ln -sfn /opt/homebrew/opt/docker-compose/bin/docker-compose ~/.docker/cli-plugins/docker-compose
```

**Cannot connect to Docker daemon** — Colima is not running:
```bash
colima start --arch aarch64 --vm-type vz --vz-rosetta --cpu 4 --memory 6
```

**Web console error on first load** — MySQL is still initialising. Wait 60 seconds and refresh.

**Port conflict on 8080 or 7080**
```bash
lsof -i :8080
```
Change the mapping in `docker-compose.yml` if needed (e.g. `"8081:80"`).

**Slow performance on NetLock containers** — expected, they run under Rosetta 2 emulation. Deploy on a Linux x86\_64 host for production.
