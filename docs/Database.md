# Database

## Engine
SQL Server 2022 (Developer edition via Docker for local dev). EF Core (added in the
persistence milestone) manages schema via migrations.

## Conventions (planned)
- GUID primary keys (sync-friendly, offline-tolerant — see Fiji localization).
- Audit columns on every entity: `CreatedAt`, `UpdatedAt`, `DeletedAt`,
  `CreatedBy`, `ModifiedBy`.
- Soft delete via EF global query filter (`DeletedAt IS NULL`).
- Optimistic concurrency via `rowversion` token.
- Mapping via `IEntityTypeConfiguration<T>` in `ERP.Persistence` — no EF attributes
  on domain entities.

## Current schema (Milestone 2 — Auth)
- **ASP.NET Identity** tables with GUID keys: `AspNetUsers` (+ `FirstName`, `LastName`,
  `CreatedAt`, `DeletedAt`), `AspNetRoles`, and the join/claim tables.
- **RefreshTokens** — server-side refresh tokens with rotation (`ReplacedByToken`),
  revocation (`RevokedAt`), unique index on `Token`.
- **LoginHistory** — one row per authentication attempt (success or failure), indexed on
  `UserId` and `OccurredAt`.

Migration: `src/ERP.Persistence/Migrations/*_InitialIdentitySchema.cs`. Applied on startup
by `ApplicationDbContextInitialiser` (which also seeds roles + a bootstrap admin from
the `Seed:*` config).

Connection string: `ConnectionStrings:DefaultConnection` in `src/ERP.API/appsettings.json`.

## Reference data & inventory (Milestones 3–4)
- **Catalog:** `Currencies`, `UnitsOfMeasure`, `Categories` (self-referencing), `Products`.
- **Taxation:** `Taxes` + `TaxRates` (effective-dated; rate is data not code).
- **Organization:** `Branches`, `Warehouses`.
- **Inventory:** `InventoryItems` (unique per product+warehouse), `StockLayers` (FIFO cost
  layers), `StockMovements` (immutable ledger).

- **Sales:** `Customers`, `SalesOrders` + `SalesOrderLines`, `Invoices` + `InvoiceLines`
  (tax rate snapshotted per line), `Payments`.

Migrations: `ReferenceData`, `InventoryModule`, `SalesModule`.

> **Guid PKs are code-assigned** (`BaseEntity`). `OnModelCreating` marks `BaseEntity` Guid keys
> `ValueGenerated.Never` so EF classifies new children of tracked parents as inserts, not
> updates. Identity's own key types are left untouched.

### Migrations
```bash
dotnet ef migrations add <Name> --project src/ERP.Persistence --startup-project src/ERP.Persistence --output-dir Migrations
```
A `DesignTimeDbContextFactory` supplies the context to the EF tools.
