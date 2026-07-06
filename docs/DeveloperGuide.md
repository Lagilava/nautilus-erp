# Developer Guide

## Solution layout
```
src/
  ERP.Shared           dependency-free kernel
  ERP.Domain           entities, value objects, domain logic
  ERP.Application       CQRS handlers, validators, DTOs (MediatR/FluentValidation)
  ERP.Infrastructure    external adapters (email, cache, fiscalization stubs)
  ERP.Persistence       EF Core DbContext + IEntityTypeConfiguration<T>
  ERP.API               ASP.NET Core host, thin controllers
tests/
  ERP.UnitTests          domain/application logic + architecture guards
  ERP.IntegrationTests   API surface via WebApplicationFactory<Program>
```

## Conventions (per build instructions)
- Every entity (added later): GUID PK, `CreatedAt/UpdatedAt/DeletedAt`,
  `CreatedBy/ModifiedBy`, soft delete via global query filter, `rowversion` concurrency token.
- CQRS via MediatR; validation as FluentValidation pipeline behaviors.
- No raw SQL string concatenation — EF parameterizes.
- XML doc comments on public types/methods.

## Definition of Done (each milestone)
Builds clean (no suppressed warnings-as-errors) · tests written & passing ·
no dependency-direction violations · docs updated · Conventional Commit proposed ·
written summary of what/why/deferred.

## Adding a project
Add under `src/` or `tests/`, `dotnet sln add`, wire references respecting the
inward dependency rule, then confirm `ArchitectureSanityTests` still passes.
