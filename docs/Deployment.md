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
| Bootstrap admin | `Seed__AdminEmail` / `Seed__AdminPassword` | optional; seeds on first run |
| Allowed origins | `Cors__AllowedOrigins__0` | the SPA origin(s) |

Local secrets in Development: `dotnet user-secrets` on `src/ERP.API`.

## Hardening (M11)
- **Rate limiting**: `/api/auth` at `RateLimiting:AuthPerMinute` (default 20/min/IP), other
  endpoints at `RateLimiting:GeneralPerMinute` (200/min/IP). Disabled under the Testing env.
- **Response compression** and baseline **security headers** (`X-Content-Type-Options`,
  `X-Frame-Options`, `Referrer-Policy`) on every response.
- **HTTPS redirection** is enforced outside Development.
- **Health**: `GET /health` includes a live database connectivity check.

## Notes
- Logs roll daily to `src/ERP.API/logs/erp-*.log` (14-file retention).
- Fiscalization (FRCS/VMS) ships as a stub (`FiscalStatus: NotSubmitted`); swap in a verified
  `IFiscalizationService` adapter via DI once the spec is confirmed.
