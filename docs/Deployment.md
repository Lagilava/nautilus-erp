# Deployment & Local Dev

## Quick start (Windows, no Docker)
Double-click `run-dev.bat` at the repo root — it opens the API and client in their own
terminals and launches the browser. Or run the two commands manually (see below).

Demo sign-ins (Development only — the demo seeder creates the latter two):

| Account | Password | Role |
|---|---|---|
| `admin@erp.local` | `Admin#12345` | Administrator |
| `manager@erp.local` | `Demo#12345` | Manager |
| `staff@erp.local` | `Demo#12345` | Staff |

Use two different accounts when walking the purchasing flow — segregation of duties will
reject an approval by the same person who raised the order.

## Prerequisites
- .NET SDK 9.0 (`dotnet --version` → 9.0.x)
- Node 20+ (for the SPA)
- Docker Desktop — optional; only for SQL Server + Redis or container deployment

## Local infrastructure
```bash
docker compose up -d        # starts erp-sqlserver (1433) and erp-redis (6379)
docker compose ps           # check health
docker compose down         # stop (add -v to wipe volumes)
```
SA password and connection strings live in `src/ERP.API/appsettings.json`. For real
deployments, override via environment variables / user-secrets — never commit secrets.

## Build, test, run
```bash
dotnet build ERP.sln -c Debug
dotnet test  ERP.sln
dotnet run   --project src/ERP.API
```
Swagger UI: `https://localhost:<port>/swagger` (Development only).
Health check: `GET /health` → JSON `{ status, checks[], totalDurationMs }`.

## Local development without Docker (SQLite)
For a zero-infrastructure run, the API uses **SQLite** in the Development environment
(`appsettings.Development.json` sets `Database:Provider = Sqlite`). No SQL Server needed —
the schema is created from the model on first run and the admin is seeded automatically.

```
dotnet run --project src/ERP.API
```
This listens on `http://localhost:5126` (default `http` launch profile). Switch back to
SQL Server by setting `Database:Provider` to `SqlServer` (uses `ConnectionStrings:DefaultConnection`)
and running `docker compose up -d` for the SQL Server + Redis containers.

> Windows `cmd.exe` note: do not paste the `#` comments from these code blocks onto a command
> line — cmd treats them as arguments, not comments. Run each command on its own.

## Frontend (client/)
React + TypeScript + Vite SPA. Requires Node 20+.
```
cd client
npm install
npm run dev
```
`npm run dev` serves `http://localhost:5173` and proxies `/api` + `/hubs` to the API at
`http://localhost:5126` (see `vite.config.ts`), so the browser talks same-origin over plain
HTTP — no CORS or dev-cert friction. Override the target with `VITE_API_TARGET`.
`npm run build` type-checks (`tsc -b`) and builds to `client/dist`.

Run the API alongside it, then sign in with the seeded admin
(`admin@erp.local` / `Admin#12345`).

## Production (containers)
Two Dockerfiles are provided:
- **API** — root `Dockerfile` (multi-stage SDK→ASP.NET runtime, binds `:8080`).
- **Client** — `client/Dockerfile` (Vite build → nginx; `client/nginx.conf` serves the SPA
  and proxies `/api` + `/hubs` to the `api` service).

```
docker build -t nautilus-api .
docker build -t nautilus-client ./client
```

### Required secrets (supply via env vars — never commit)
| Setting | Env var | Notes |
|---------|---------|-------|
| JWT signing key | `Jwt__SigningKey` | ≥ 32 chars. **Startup fails** outside Development if it's the dev default or too short. |
| SQL connection | `ConnectionStrings__DefaultConnection` | with `Database__Provider=SqlServer` |
| Bootstrap admin | `Seed__AdminEmail` / `Seed__AdminPassword` | Optional. **No administrator is seeded unless you set these** — the demo credentials live in `appsettings.Development.json` and never load in Production. Startup fails outside Development if the password is weak or is the demo one. |
| Allowed origins | `Cors__AllowedOrigins__0` | the SPA origin(s) |
| Allowed hosts | `AllowedHosts` | your deployment hostname, to reject host-header spoofing |

Local secrets in Development: `dotnet user-secrets` on `src/ERP.API`.

> **Why the seeded admin is not in `appsettings.json`.** That file loads in *every* environment.
> A password there is a known-credential administrator on any public deployment — the first thing
> an automated scanner tries. Development values live in `appsettings.Development.json`.

## Hardening (M11)
- **Behind a proxy** (Render, nginx, any load balancer) `UseForwardedHeaders` runs first, so
  `X-Forwarded-Proto`/`-For` are honoured. Without it the HTTPS redirect loops and — worse —
  every client shares one rate-limit partition, turning the brute-force defence into a DoS.
  It trusts those headers unconditionally, so **never expose the container directly**.
- **Rate limiting**: `/api/auth` at `RateLimiting:AuthPerMinute` (default 20/min/IP), other
  endpoints at `RateLimiting:GeneralPerMinute` (200/min/IP). Disabled under the Testing env.
- **Response compression** and **security headers** (`X-Content-Type-Options`, `X-Frame-Options`,
  `Referrer-Policy`, `Permissions-Policy`, and a `default-src 'none'` CSP outside Development).
  The SPA's own CSP lives in `client/nginx.conf`, since nginx serves the HTML.
- **HTTPS redirection** and **HSTS** are enforced outside Development.
- **Health**: `GET /health` includes a live database connectivity check.
- **Session revocation**: deactivating a user revokes their live refresh tokens immediately, and
  replaying a rotated refresh token revokes the entire token family (reuse detection).

## Notes
- Logs roll daily to `src/ERP.API/logs/erp-*.log` (14-file retention).
- Fiscalization (FRCS/VMS) ships as a stub (`FiscalStatus: NotSubmitted`); swap in a verified
  `IFiscalizationService` adapter via DI once the spec is confirmed.
