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

## Fiji localization (design intent)
Tax/fiscalization are first-class Domain concerns, not late add-ons. See
[erp-claude-code-prompt.md](../erp-claude-code-prompt.md) → *Fiji Localization Requirements*.
`IFiscalizationService` (FRCS TPOS/VMS) will be an Application port with a stub
Infrastructure adapter until the real spec is verified.

## Milestone 1 status (current)
Solution scaffold only: Clean Architecture skeleton, Serilog, Swagger, `/health`,
Docker Compose (SQL Server + Redis). No business logic yet.
