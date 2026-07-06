# API Reference

Base URL (dev): `https://localhost:<port>`

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
stored server-side, and **rotated on every refresh** (reuse of a consumed token is rejected).

| Method | Route | Auth | Body | Success |
|--------|-------|------|------|---------|
| POST | `/api/auth/register` | anon | `{ email, password, firstName, lastName }` | 200 `AuthenticationResult` |
| POST | `/api/auth/login` | anon | `{ email, password }` | 200 `AuthenticationResult` |
| POST | `/api/auth/refresh` | anon | `{ refreshToken }` | 200 `AuthenticationResult` |
| POST | `/api/auth/logout` | bearer | `{ refreshToken }` | 204 |
| POST | `/api/auth/forgot-password` | anon | `{ email }` | 200 (token, never reveals existence) |
| POST | `/api/auth/reset-password` | anon | `{ email, token, newPassword }` | 204 |
| GET  | `/api/auth/me` | bearer | — | 200 `UserIdentity` |

`AuthenticationResult`: `{ userId, email, roles[], accessToken, accessTokenExpiresAt, refreshToken }`.

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

---
Business endpoints (sales, purchase, …) are documented here as each module lands.
