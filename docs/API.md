# API Reference

Base URL (dev): `https://localhost:<port>`

## Conventions (for the SPA client)
- **Enums are strings** (e.g. `"status": "Issued"`, `"fiscalStatus": "NotSubmitted"`).
  Integer values are still accepted on input for convenience.
- **Errors** are RFC 7807 problem-details; validation failures carry a field-keyed `errors`
  object. Status mapping: 400 validation, 401 unauthorized, 404 not found, 409 conflict,
  **423 account locked**.
- **Pagination**: list endpoints return `{ items, page, pageSize, totalCount, totalPages, hasNextPage, hasPreviousPage }`.
- **Dates**: `DateOnly` fields are ISO date strings (`"2026-07-01"`).
- **CORS**: allowed SPA origins come from `Cors:AllowedOrigins` (default `http://localhost:5173`,
  `http://localhost:3000`); credentials are allowed so the SignalR hub can authenticate.
- **Auth**: `POST /api/auth/login` returns `accessToken` (15 min) + `refreshToken` (rotated)
  in the body, or an MFA challenge if the account has two-factor enabled (see below); send
  `Authorization: Bearer <accessToken>`; refresh via `POST /api/auth/refresh`.

## Endpoints

### `GET /health`
Liveness/readiness probe.

**200 OK**
```json
{
  "status": "Healthy",
  "checks": [],
  "totalDurationMs": 0.12
}
```
`checks[]` gains SQL Server and Redis entries in the persistence milestone.

### Swagger
`GET /swagger` (Development only) — interactive OpenAPI UI.

## Authentication (`/api/auth`)

JWT bearer. Access tokens are short-lived (15 min); refresh tokens are long-lived (7 days),
stored server-side (SHA-256 hashed), and **rotated on every refresh** (reuse of a consumed
token is rejected).

There is **no public registration**. Accounts are created by an administrator via
`POST /api/users` — this is a staff system, and customers never sign in.

| Method | Route | Auth | Body | Success |
|--------|-------|------|------|---------|
| POST | `/api/auth/login` | anon | `{ email, password }` | 200 `LoginResult` |
| POST | `/api/auth/mfa/verify` | anon | `{ challengeToken, code }` | 200 `AuthenticationResult` |
| POST | `/api/auth/refresh` | anon | `{ refreshToken }` | 200 `AuthenticationResult` |
| POST | `/api/auth/logout` | bearer | `{ refreshToken }` | 204 |
| POST | `/api/auth/forgot-password` | anon | `{ email }` | 204 always — emails the link, never reveals whether the account exists |
| POST | `/api/auth/reset-password` | anon | `{ email, token, newPassword }` | 204 |
| GET  | `/api/auth/me` | bearer | — | 200 `UserIdentity` |
| PUT  | `/api/auth/me` | bearer | `{ firstName, lastName }` | 204 |
| POST | `/api/auth/change-password` | bearer | `{ currentPassword, newPassword }` | 204 |
| POST | `/api/auth/mfa/setup` | bearer | — | 200 `MfaSetup` — (re)generates an authenticator secret, not yet active |
| POST | `/api/auth/mfa/enable` | bearer | `{ code }` | 200 `string[]` — confirms setup, turns MFA on, returns one-time recovery codes |
| POST | `/api/auth/mfa/disable` | bearer | `{ currentPassword }` | 204 |

`AuthenticationResult`: `{ userId, email, roles[], accessToken, accessTokenExpiresAt, refreshToken }`.

`LoginResult`: `{ mfaRequired, mfaChallengeToken, tokens }` — when `mfaRequired` is false, `tokens`
is an `AuthenticationResult` and `mfaChallengeToken` is null; when true, `tokens` is null and
`mfaChallengeToken` must be redeemed via `POST /api/auth/mfa/verify` (TOTP or a recovery code)
to receive tokens. The challenge token is a signed, 5-minute-lived JWT scoped to a distinct
audience — it cannot be used as a bearer token against any other endpoint.

`MfaSetup`: `{ sharedKey, authenticatorUri }` — `sharedKey` is the raw Base32 secret for manual
entry; `authenticatorUri` is the `otpauth://` URI an authenticator app QR-scans.

**Failure mapping** (`Result.Error.Code` → HTTP): `validation`→400, `unauthorized`→401,
`locked_out`→423, `conflict`→409, `not_found`→404. FluentValidation failures return 400
problem-details with a field-keyed `errors` object.

**Security:** password complexity (8+, upper/lower/digit/symbol), account lockout
(5 failed attempts → 15 min), login history recorded for every attempt, no account enumeration.

## Reference data (Milestone 3)

All require authentication. Writes require `Administrator` or `Manager`.

| Resource | Routes |
|----------|--------|
| Products | `GET/POST /api/products`, `GET/PUT/DELETE /api/products/{id}` (paged list: `?page&pageSize&search`) |
| Taxes | `GET/POST /api/taxes`, `POST /api/taxes/{id}/rates` (effective-dated) |
| Currencies | `GET/POST /api/currencies` |
| Units of measure | `GET/POST /api/units-of-measure` |
| Categories | `GET/POST /api/categories` |
| Branches | `GET/POST /api/branches` |
| Warehouses | `GET/POST /api/warehouses` |

**Tax rates are data, not code:** a `Tax` has `Standard`/`ZeroRated`/`Exempt` treatment;
`Standard` taxes carry effective-dated `TaxRate` rows. `POST /api/taxes/{id}/rates` closes
the current open rate and opens a new one, so historical documents resolve the rate that
applied on their date. Reads expose `currentRate` (in force today) plus the full history.

## Inventory (Milestone 4)

Reads: any authenticated user. Stock-changing operations: `Administrator`/`Manager`.
**Costing is FIFO** — issues consume the earliest-received cost layers; every change is an
immutable ledger entry.

| Method | Route | Purpose |
|--------|-------|---------|
| POST | `/api/inventory/receive` | Goods in at a unit cost (new FIFO layer) |
| POST | `/api/inventory/issue` | Goods out; records FIFO COGS |
| POST | `/api/inventory/transfer` | Move between warehouses, preserving cost |
| POST | `/api/inventory/adjust` | Stock-take correction (+/−), reason required |
| PUT  | `/api/inventory/reorder-level` | Set reorder threshold |
| GET  | `/api/inventory/levels` | Paged stock levels + valuation (`?warehouseId&lowStockOnly&page&pageSize`) |
| GET  | `/api/inventory/movements` | Paged ledger (`?productId&warehouseId&page&pageSize`) |

Insufficient stock on issue/transfer/adjust returns **409 Conflict**. Stock value on a level
is the sum of remaining FIFO layers (`Σ remainingQty × unitCost`).

## Sales (Milestone 5)

Reads: any authenticated user. Writes: `Administrator`/`Manager`.

| Method | Route | Purpose |
|--------|-------|---------|
| GET/POST | `/api/customers` | List / create customers |
| GET/POST | `/api/sales-orders` (+ `/{id}`) | List / create / view orders |
| POST | `/api/sales-orders/{id}/confirm\|fulfill\|cancel` | Order state machine (fulfil issues stock) |
| POST | `/api/invoices/from-order` | Raise a draft invoice from an order (snapshots VAT) |
| POST | `/api/invoices/{id}/issue\|void` | Invoice state machine (issue submits to fiscalization) |
| GET | `/api/invoices` (+ `/{id}`) | List / view invoices with totals + fiscal status |
| GET/POST | `/api/invoices/{id}/payments` | List / record payments (advances to PartiallyPaid/Paid) |

**State machines** (illegal transitions → 409): order `Draft→Confirmed→Fulfilled`, `→Cancelled`;
invoice `Draft→Issued→PartiallyPaid→Paid`, `→Void` (only before payment). Fulfilment is
all-or-nothing and issues FIFO stock; insufficient stock → 409, order stays Confirmed.

**Fiji fiscalization:** issuing an invoice calls `IFiscalizationService`. The shipped stub
records `fiscalStatus: NotSubmitted` — the FRCS/VMS integration is unverified and is **not
faked**. Swapping in a verified adapter is a DI change only.

**Tax on invoices is snapshotted** at the rate in force on the issue date (via the
effective-dated tax engine), so an issued invoice never changes retroactively.

## Purchasing (Milestone 6)

Reads: any authenticated user. Writes: `Administrator`/`Manager`.

| Method | Route | Purpose |
|--------|-------|---------|
| GET/POST | `/api/suppliers` | List / create suppliers |
| GET/POST | `/api/purchase-orders` (+ `/{id}`) | List / create / view POs |
| POST | `/api/purchase-orders/{id}/confirm\|cancel` | PO state machine |
| POST | `/api/purchase-orders/{id}/receipts` | Post a goods receipt (**receives FIFO stock**) |
| POST | `/api/supplier-invoices/from-order` | Bill from a PO (input-VAT snapshot) |
| POST | `/api/supplier-invoices/{id}/approve\|cancel` | AP invoice state machine |
| GET | `/api/supplier-invoices` (+ `/{id}`) | List / view supplier invoices |
| POST | `/api/supplier-invoices/{id}/payments` | Record a payment to the supplier |

**Goods receipt is the mirror of sales fulfilment:** posting one receives stock into the
warehouse (a FIFO cost layer per line at the PO unit cost) and advances the PO to
`PartiallyReceived`/`Received`. Over-receiving a line → 409, nothing posted. Partial
receipts across multiple deliveries are supported (`QuantityReceived`/`OutstandingQuantity`).

## Dashboard & Reports (Milestone 8)

- `GET /api/dashboard` — KPIs: customer/supplier/product counts, inventory value, low-stock
  count, sales this month, accounts receivable/payable, open sales/purchase orders.
- `GET /api/reports/inventory-valuation?warehouseId&format=` — export the valuation report.
  `format`: `1` CSV, `2` Excel (xlsx), `3` PDF. Returns a file download.

Reports are provider-agnostic: a query builds a `ReportTable`, and `IReportExporter`
(CSV native, Excel via ClosedXML, PDF via QuestPDF) renders the requested format.

## Notifications (Milestone 9)

- **SignalR hub:** `/hubs/notifications` (authenticated). Clients handle the `notification`
  method; each connection joins a per-user group so messages can target one user.
- **Email queue:** business events enqueue emails onto **Hangfire** (in-memory storage) for
  background delivery with retries. The shipped `IEmailSender` is a logging stub — swap in
  SMTP via DI. Issuing an invoice publishes a real-time notification and queues a customer email.

---
Frontend (M10) and hardening (M11) follow.
