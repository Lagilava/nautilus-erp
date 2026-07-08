# Architecture

## Overview

ERP is a Clean Architecture .NET 9 solution. Dependencies point inward; the Domain
knows nothing of frameworks or persistence.

```
ERP.Shared        (no dependencies — result types, primitives, constants)
   ▲
ERP.Domain        (entities, value objects, domain rules; refs Shared only)
   ▲
ERP.Application   (CQRS via MediatR, FluentValidation, DTOs; refs Domain, Shared)
   ▲
ERP.Infrastructure / ERP.Persistence   (EF Core, external adapters; ref Application)
   ▲
ERP.API           (thin controllers, HTTP ↔ MediatR; refs Application, Infra, Persistence)
```

### Enforced rules
- **Dependency direction** is asserted by `ArchitectureSanityTests` in `ERP.UnitTests` —
  the Domain assembly must not reference Application/Infrastructure/Persistence.
- **Controllers are thin** — HTTP translation only, no business logic or DbContext.
- **Entities are persistence-ignorant** — no EF attributes; `IEntityTypeConfiguration<T>`
  in Persistence (added in the persistence milestone).

## Access control
Three layers, applied in order:

1. **Role-based** — `Administrator`, `Manager`, `Staff`, via `[Authorize(Roles = …)]`.
   Customers never sign in; the ERP is a staff system. Role and duty are different axes:
   Staff hold the *warehouse* duty (they post goods receipts) but approving spend — raising,
   confirming, or cancelling a purchase order — stays with Managers and Administrators.
2. **Branch scoping** (record-level) — a `branch` JWT claim narrows every warehouse-bound
   query and write through `IBranchScope`. A user with no branch is unrestricted.
3. **Segregation of duties** (`Common/Security/SegregationOfDuties.cs`) — maker-checker on the
   procure-to-pay chain, so no one person can raise an order, approve it, receive the goods,
   approve the bill and pay it. The rules:

   | Rule | The actor may not be… |
   |---|---|
   | `PurchaseOrderApproval` | the person who raised the order |
   | `GoodsReceipt` | the raiser or the approver of the order |
   | `SupplierInvoiceApproval` | the person who entered the bill, or who received the goods |
   | `SupplierPayment` | the person who approved the bill |
   | `InvoiceVoid` | the person who issued the invoice |

   Enforced by default and returning **403** with an explanatory message. Every block is logged.
   A two-person shop can relax individual rules — or all of them — through configuration:

   ```jsonc
   "Sod": { "Enabled": true, "DisabledRules": ["SupplierPayment"] }
   ```

   Documents created without an authenticated user (the demo seeder) have no recorded actor and
   therefore never trip a rule.

## Fiji localization (design intent)
Tax/fiscalization are first-class Domain concerns, not late add-ons. See
[erp-claude-code-prompt.md](../erp-claude-code-prompt.md) → *Fiji Localization Requirements*.
`IFiscalizationService` (FRCS TPOS/VMS) will be an Application port with a stub
Infrastructure adapter until the real spec is verified.

## Module status
- **M1 Scaffold** · **M2 Auth** (JWT + refresh rotation, lockout, login history)
- **M3 Reference data** (products, effective-dated taxes, org, catalog)
- **M4 Inventory** (FIFO layers, ledger, valuation)
- **M5 Sales** (customers, order & invoice state machines, payments, fiscalization port)
- **M6 Purchasing** (suppliers, PO state machine, goods receipt → FIFO stock, supplier invoices/payments)
- **M7 Audit logging** (SaveChanges interceptor → `AuditLogs`, admin-only trail)
- **M8 Dashboard & reporting** (KPIs + CSV/Excel/PDF export via `IReportExporter`)
- **M9 Notifications** (SignalR hub `IRealtimeNotifier` + Hangfire email queue `IEmailQueue`/`IEmailSender`)
- **M10 React SPA** (Vite + TS, Lagoon design system; auth, dashboard, catalog, order-to-cash, procure-to-pay, admin)
- **M11 Hardening** (rate limiting, response compression, security headers, DB health check, JWT-secret guard, Docker)
- **System administration** (Administrator-only user/role management + reference-data settings)
- **M10 Frontend** (`client/` — React + TS + Vite SPA; TanStack Query, Axios w/ token refresh,
  Tailwind "Lagoon" design system; auth, dashboard, products, customers, suppliers, inventory,
  reports, audit)

Auditing is cross-cutting: `AuditSaveChangesInterceptor` (Persistence) records every
insert/update/delete of a `BaseEntity`, attached via `OnConfiguring` so it applies to
every provider including tests. `GET /api/audit-logs` is Administrator-only.

`IFiscalizationService` (Application port) is implemented by `NullFiscalizationService`
(Infrastructure) — a deliberate stub until the FRCS/VMS spec is verified.
