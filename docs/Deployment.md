# Deployment & Local Dev

## Prerequisites
- .NET SDK 9.0 (`dotnet --version` → 9.0.x)
- Docker Desktop (for SQL Server + Redis)

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

## Frontend (client/)
React + TypeScript + Vite SPA. Requires Node 20+.
```bash
cd client
npm install
npm run dev      # http://localhost:5173 (proxies /api and /hubs to https://localhost:7203)
npm run build    # type-check (tsc -b) + production build to client/dist
```
The dev server proxies to the API's HTTPS dev port (`vite.config.ts`), so the browser talks
same-origin — no CORS/cert friction in dev. Override the target with `VITE_API_TARGET`.
Run the API (`dotnet run --project src/ERP.API`) alongside it. Sign in with the seeded admin
(`admin@erp.local` / `Admin#12345`).

## Notes
- Data-store health checks (SQL/Redis) are added in the persistence milestone.
- Logs roll daily to `src/ERP.API/logs/erp-*.log` (14-file retention).
