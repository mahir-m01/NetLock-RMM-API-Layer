# ControlIT Release Package

## Package

ControlIT alpha is shipped as a Docker Compose source package from the `production` branch and GitHub Releases.

Release version: `v0.1.0-alpha.1`

## Included

| Path | Purpose |
|---|---|
| `README.md` | Install-first release README. |
| `RELEASE.md` | Operations, environment, rotation, and update guide. |
| `CHANGELOG.md` | Release history. |
| `.env.example` | Required environment reference. |
| `docker-compose.yml` | ControlIT API and dashboard deployment. |
| `Dockerfile` | API container build. |
| `Dockerfile.web` | Dashboard container build. |
| `scripts/setup-controlit-env.sh` | Generates `.env` and bootstrap credentials. |
| `scripts/discover-netlock-env.sh` | Discovers standard same-host NetLock Docker values. |
| `scripts/install-controlit.sh` | Applies migrations, creates runtime DB user, starts services, checks readiness. |
| `scripts/update-controlit.sh` | Fast-forward update and restart flow. |
| `scripts/install-controlit-ota.sh` | Optional systemd OTA timer. |
| `scripts/check-controlit-update.sh` | Remote update check. |
| `src/ControlIT.Api/` | ASP.NET Core API source. |
| `src/ControlIT.Web/` | Next.js dashboard source and brand assets. |

## Not Included

- NetLock installer or NetLock source.
- NetBird installer binaries.
- Customer secrets, tokens, passwords, or `.env`.
- Development diagrams, AI workflow contracts, graph files, test fixtures, or local demo VM config.

## Install Contract

ControlIT install assumes:

1. NetLock is already installed and healthy.
2. NetLock MySQL is reachable by Docker network or configured host.
3. NetLock SignalR command hub is reachable from ControlIT API.
4. NetBird Management API URL and token are provided.
5. Browser-facing ControlIT API URL and allowed origin are configured.

## Verification Gate

Installer must finish with:

```bash
curl -f http://localhost:5290/health/ready
```

Expected containers:

```bash
docker compose ps
```

- `controlit-api`
- `controlit-web`

## Update Contract

Updates use:

```bash
./scripts/update-controlit.sh
```

The script backs up `.env`, fast-forwards `production`, applies ControlIT migrations, refreshes DB grants, rebuilds containers, and checks readiness.
