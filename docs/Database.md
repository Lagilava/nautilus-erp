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

- **Purchasing:** `Suppliers`, `PurchaseOrders` + `PurchaseOrderLines` (track
  `QuantityReceived`), `GoodsReceipts` + `GoodsReceiptLines`, `SupplierInvoices` +
  `SupplierInvoiceLines`, `SupplierPayments`.

Migrations: `ReferenceData`, `InventoryModule`, `SalesModule`, `PurchasingModule`.

## General ledger / accounting (Milestone 12)
- **Chart of accounts:** `Accounts` (`Code`, `Name`, `Type` — Asset/Liability/Equity/Revenue/
  Expense, `IsSystem`, `IsActive`). Seven system accounts are seeded on startup: 1000 Cash,
  1100 Accounts Receivable, 1200 Inventory, 2100 Accounts Payable, 2200 Sales Tax Payable,
  4000 Sales Revenue, 5000 Cost of Goods Sold.
- **Journal:** `JournalEntries` (`BranchId`, `EntryDate`, `Reference`, `Description`,
  `Status` — Draft/Posted/Voided, `Source` — Manual/SalesInvoice/SupplierInvoice/Payment,
  `SourceDocumentId`, `PreparedBy`, `PostedBy`) + `JournalLines` (`AccountId`, `Debit`,
  `Credit`, `Memo`, plus nullable `CurrencyId`/`ExchangeRate` for multi-currency lines).
  Account balances are never cached — the trial balance/P&L/balance sheet reports sum
  `JournalLines` grouped by account/type on read, mirroring how `Invoice.Balance` is a
  derived expression today.
- **Period locking:** `AccountingPeriods` (`Year`, `Month`, `IsClosed`, `ClosedBy`,
  `ClosedAt`). Both manual entry creation and auto-posting reject any entry dated inside a
  closed period.
- **Bank reconciliation:** `BankStatementLines` (`StatementDate`, `Amount`, `Description`,
  `Source` — Imported/Manual, `MatchedJournalLineId`) + `Reconciliations` (one row per match,
  audit trail of `MatchedJournalLineId`/`MatchedAt`/`MatchedBy`).

Migrations: `GeneralLedgerModule`, `PeriodLockingAndReconciliation` (authored for both the
default SQL Server provider under `src/ERP.Persistence/Migrations` and the Postgres provider
under `src/ERP.Persistence.Migrations.Postgres/Migrations`).

> **Guid PKs are code-assigned** (`BaseEntity`). `OnModelCreating` marks `BaseEntity` Guid keys
> `ValueGenerated.Never` so EF classifies new children of tracked parents as inserts, not
> updates. Identity's own key types are left untouched.

### Migrations
```bash
dotnet ef migrations add <Name> --project src/ERP.Persistence --startup-project src/ERP.Persistence --output-dir Migrations
```
A `DesignTimeDbContextFactory` supplies the context to the EF tools.
