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

## Notes
- Data-store health checks (SQL/Redis) are added in the persistence milestone.
- Logs roll daily to `src/ERP.API/logs/erp-*.log` (14-file retention).
