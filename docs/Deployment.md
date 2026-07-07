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

## Notes
- Data-store health checks (SQL/Redis) are added in the persistence milestone.
- Logs roll daily to `src/ERP.API/logs/erp-*.log` (14-file retention).
