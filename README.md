# NetLock RMM — Local Development Setup

Local Docker stack for NetLock RMM on **Apple Silicon (M-series)** using Colima.

---

## Stack

| Container | Image | Port | Purpose |
|---|---|---|---|
| `mysql-container` | `mysql:8.0` | `3306` | Database (native arm64) |
| `netlock-rmm-server` | `nicomak101/netlock-rmm-server` | `7080` (HTTP), `7082` (TCP relay) | Backend — all server roles |
| `netlock-rmm-web-console` | `nicomak101/netlock-rmm-web-console` | `8080` | Blazor admin web UI |
> The two NetLock images are **amd64-only**. They run under Rosetta 2 emulation via Colima's `--vz-rosetta` flag. MySQL runs natively on arm64.
>
> **Note on relay port:** The relay is mapped to host port `7082` (not `7081`) because macOS SSH port forwarding holds `7081` on this machine. The container still listens internally on `7081`.

---

## Prerequisites

Install Colima, Docker CLI, and the Docker Compose plugin:

```bash
brew install colima docker docker-compose
```

Wire the Compose plugin to Docker CLI (one-time):

```bash
mkdir -p ~/.docker/cli-plugins
ln -sfn /opt/homebrew/opt/docker-compose/bin/docker-compose ~/.docker/cli-plugins/docker-compose
```

Verify:

```bash
docker compose version
# Docker Compose version 5.x.x
```

---

## 1. Start Colima

Run this after every Mac restart (Colima does not auto-start):

```bash
colima start --arch aarch64 --vm-type vz --vz-rosetta --cpu 4 --memory 6
```

> **Note:** the flag is `--vz-rosetta`, not `--rosetta`. The `--rosetta` flag does not exist in this version of Colima.

| Flag | Why |
|---|---|
| `--arch aarch64` | Native ARM64 VM — fast |
| `--vm-type vz` | Apple Virtualization Framework — much faster than QEMU |
| `--vz-rosetta` | Enables Rosetta 2 inside the VM to run amd64 containers |
| `--cpu 4` | Allocate 4 cores (adjust to taste) |
| `--memory 6` | 6 GB RAM — needed for MySQL + two .NET containers |

To stop Colima: `colima stop`

To check status: `colima status`

---

## 2. Configure your API key

Copy the example env file:

```bash
cp .env.example .env
```

Edit `.env` and set your values — especially `NETLOCK_API_KEY` from [members.netlockrmm.com](https://members.netlockrmm.com).

The `appsettings.json` files in `config/` contain only structural defaults. All secrets are injected at runtime from `.env` via Docker Compose environment variables — ASP.NET Core env vars override the JSON config automatically.

---

## 3. Start the stack

```bash
cd /path/to/NetLock-RMM-API-Layer
docker compose up -d
```

First run pulls ~350 MB of images. MySQL runs a healthcheck before the NetLock containers start — they will wait until MySQL is fully ready. Allow **2–3 minutes** on first boot for the schema to be created.

---

## 4. Access

| Service | URL | Default credentials |
|---|---|---|
| NetLock Web Console | http://localhost:8080 | `admin` / `admin` |
| NetLock Server API | http://localhost:7080 | — |
| MySQL | `localhost:3306` | root / see `.env` |

---

## 5. Useful commands

```bash
# Check all container statuses
docker compose ps

# Stream logs for all containers
docker compose logs -f

# Stream logs for a specific container
docker compose logs -f netlock-rmm-server

# Stop the stack (data is preserved)
docker compose down

# Restart a single container
docker compose restart netlock-rmm-server

# Pull latest images
docker compose pull && docker compose up -d

# Full reset — wipes all data
docker compose down -v && rm -rf data/mysql data/server data/web_console
docker compose up -d
```

---

## 6. Test Agent — Debian Lima VM

A lightweight Debian 12 (ARM64) Lima VM is included for testing the NetLock Linux agent locally.

### Prerequisites

```bash
brew install lima
```

### Start the VM

```bash
limactl start debian-test.yaml
```

> First start downloads the Debian cloud image (~300 MB) and runs the provision script.

### Shell into the VM

```bash
limactl shell debian-test
```

### Install the NetLock agent

Inside the VM — download the installer from the NetLock web console:

1. Go to `http://localhost:8080` → Download Installer
2. Set all server fields to `192.168.5.2:7080` (the Mac host gateway — fixed by Lima)
3. Set Relay to `192.168.5.2:7082`
4. Select architecture: **linux-arm64**
5. Select your Tenant and Location, then copy the download URL

```bash
wget -O installer.zip '<download-url-with-guid-and-password>'
unzip installer.zip
chmod +x NetLock_RMM_Agent_Installer
sudo ./NetLock_RMM_Agent_Installer
```

The agent will appear in the NetLock web console under your selected tenant/location.

> **Note:** `192.168.5.2` is Lima's fixed host gateway — it always resolves to your Mac from inside any Lima VM.

### Stop / delete the VM

```bash
limactl stop debian-test
limactl delete debian-test   # wipes disk — use to recreate clean
```

---

## Folder Structure

```
NetLock-RMM-API-Layer/
├── docker-compose.yml          # Main compose file
├── debian-test.yaml            # Lima VM config for agent testing
├── .env                        # Secrets — gitignored, never commit
├── .env.example                # Template — commit this
├── config/
│   ├── server/
│   │   └── appsettings.json    # NetLock server structural config (secrets via env)
│   └── web_console/
│       └── appsettings.json    # Web console structural config (secrets via env)
├── data/                       # Gitignored — written by containers at runtime
│   ├── mysql/
│   ├── server/
│   ├── web_console/
│   └── certificates/
└── .ai-workflow/               # Gitignored — architecture docs, UML diagrams, agent handoff, contracts
```

---

## Troubleshooting

**`docker compose` not found after installing**
Wire the plugin manually:
```bash
mkdir -p ~/.docker/cli-plugins
ln -sfn /opt/homebrew/opt/docker-compose/bin/docker-compose ~/.docker/cli-plugins/docker-compose
```

**`cannot connect to the Docker daemon`**
Colima is not running. Start it:
```bash
colima start --arch aarch64 --vm-type vz --vz-rosetta --cpu 4 --memory 6
```

**Web console shows error on first load**
MySQL takes 30–60 seconds to finish schema setup on first boot. Wait and refresh.

**Port conflict on 8080 or 7080**
```bash
lsof -i :8080   # find what's using it
```
Then change the port mapping in `docker-compose.yml` (e.g. `"8081:80"`).

**Slow performance on NetLock containers**
Expected — they run under Rosetta 2 x86 emulation. For production, deploy on a Linux x86_64 VPS.
