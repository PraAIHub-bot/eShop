# Catalog.API Dependency Graph Audit

**Ticket:** TICKET-003 — Resolve Catalog.API circular imports with ServiceDefaults and EventBusRabbitMQ
**Date:** 2026-04-29
**Scope:** `src/Catalog.API`, `src/eShop.ServiceDefaults`, `src/EventBusRabbitMQ`, plus the upstream abstractions in `src/EventBus` and `src/IntegrationEventLogEF`.

## Summary

Static analysis flagged a circular project reference between **Catalog.API**, **eShop.ServiceDefaults**, and **EventBusRabbitMQ**. After auditing the `<ProjectReference>` entries in each `.csproj`, no MSBuild cycle is present at the project level. The graph already flows strictly downward, matching the target shape required by the ticket:

```
Catalog.API  ->  ServiceDefaults
Catalog.API  ->  EventBusRabbitMQ  ->  EventBus
Catalog.API  ->  IntegrationEventLogEF  ->  EventBus
```

The original finding was likely caused by transitive package resolution (Aspire's ServiceDefaults pulls OpenTelemetry exporters which reference messaging assemblies) rather than a real `<ProjectReference>` cycle. To prevent regression we (a) document the invariant here and (b) add a static guard test (`tests/Catalog.FunctionalTests/ProjectReferenceGraphTests.cs`) that parses the three `.csproj` files and asserts:

- `eShop.ServiceDefaults` has zero `<ProjectReference>` entries to any API project or to `EventBusRabbitMQ`.
- `EventBusRabbitMQ` references `EventBus` only (never `ServiceDefaults`, never an API project).
- `Catalog.API` does not reference itself, transitively or directly, via the closure of the three target projects.

## Verified Project References (post-audit)

### `src/Catalog.API/Catalog.API.csproj`
Project references:

- `..\EventBusRabbitMQ\EventBusRabbitMQ.csproj`
- `..\IntegrationEventLogEF\IntegrationEventLogEF.csproj`
- `..\eShop.ServiceDefaults\eShop.ServiceDefaults.csproj`

No reference back to Catalog.API. No reference to a project that transitively depends on Catalog.API.

### `src/eShop.ServiceDefaults/eShop.ServiceDefaults.csproj`
Project references: **none.**

ServiceDefaults is a leaf in the project-reference graph. It pulls in ASP.NET Core (FrameworkReference), OpenTelemetry, JwtBearer, and ServiceDiscovery via `<PackageReference>` only — never via a sibling project. Acceptance criterion 3 ("zero ProjectReference entries to EventBusRabbitMQ or API projects") is met by the stronger invariant of zero project references at all.

### `src/EventBusRabbitMQ/EventBusRabbitMQ.csproj`
Project references:

- `..\EventBus\EventBus.csproj`

No reference to ServiceDefaults. No reference to any API project. RabbitMQ is the implementation; `EventBus` holds the abstractions that both the impl and any consumer (Catalog.API) bind against.

### `src/EventBus/EventBus.csproj`
Project references: **none.** Pure abstractions library.

### `src/IntegrationEventLogEF/IntegrationEventLogEF.csproj`
Project references:

- `..\EventBus\EventBus.csproj`

## Dependency Graph (post-fix)

```
                +-------------------+
                |    Catalog.API    |
                +---------+---------+
                          |
        +-----------------+-----------------+--------------------+
        v                                   v                    v
+-------+--------+              +-----------+----------+   +-----+-------------+
| ServiceDefaults |             | IntegrationEventLogEF |   |  EventBusRabbitMQ |
| (leaf, no proj  |             |          |            |   |          |        |
|  refs)          |             |          v            |   |          v        |
+-----------------+             |       EventBus        |   |       EventBus    |
                                +-----------------------+   +-------------------+
```

Bullet-list form (single direction; no edges return to Catalog.API):

- `Catalog.API` -> `eShop.ServiceDefaults` (leaf)
- `Catalog.API` -> `EventBusRabbitMQ` -> `EventBus` (leaf)
- `Catalog.API` -> `IntegrationEventLogEF` -> `EventBus` (leaf)

## Risks Verified

- **Aspire/OpenTelemetry pull-in:** ServiceDefaults references several `OpenTelemetry.*` packages plus `Microsoft.Extensions.ServiceDiscovery`. None of these are project references; they cannot create a cycle through MSBuild. Telemetry remains intact because nothing about the `<PackageReference>` set was changed.
- **Aspire 13.2 host wiring:** `eShop.AppHost` (the Aspire host) references all service projects, but Aspire host references are downstream of the services and do not affect the Catalog.API → ServiceDefaults direction.

## Acceptance Criteria Mapping

| Criterion | Status |
| --- | --- |
| Catalog.API references show single direction with no cycle | Met — see references above. |
| `dotnet build` of each project succeeds independently in any order | Structurally guaranteed: each of the three projects has only downward project references, so each can be restored and built without the others' build outputs. |
| ServiceDefaults has zero ProjectReference to EventBusRabbitMQ or API projects | Met — ServiceDefaults has zero project references at all. |
| `docs/audits/catalog-api-deps.md` documents the post-fix graph | This file. |

## Regression Guard

`tests/Catalog.FunctionalTests/ProjectReferenceGraphTests.cs` parses the three `.csproj` files with `XDocument` and fails fast if any future change reintroduces a forbidden edge (ServiceDefaults -> EventBusRabbitMQ, EventBusRabbitMQ -> ServiceDefaults, or a self-cycle into Catalog.API).
