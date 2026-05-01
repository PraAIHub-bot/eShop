# Audit: `src/Shared` library usage and consolidation decision

- **Ticket:** TICKET-008 — Audit Shared library usage and consolidate or remove
- **Date:** 2026-05-01
- **Author:** dev-agent
- **Status:** Decision recorded

## Recommendation

**KEEP `src/Shared/` with a documented purpose.** Do not delete; do not consolidate into
`eShop.ServiceDefaults` as part of this ticket.

## Rationale (short form)

`src/Shared/` is **not a project**. It has no `.csproj`, no entry in `eShop.slnx`, and is not
referenced via `<ProjectReference>` anywhere. It is a deliberately‑scoped folder of two
`internal static` helper source files that are pulled into consuming projects with linked
compilation:

```xml
<Compile Include="..\Shared\ActivityExtensions.cs" Link="Extensions\ActivityExtensions.cs" />
<Compile Include="..\Shared\MigrateDbContextExtensions.cs" Link="Extensions\MigrateDbContextExtensions.cs" />
```

The original analyzer finding ("Shared library limited usage") appears to have been triggered
because the analyzer treated `src/Shared` as a stand‑alone library and saw zero
`ProjectReference` edges into it. In reality the helpers are linked into **5 projects** (see
inventory below), which is broad — not limited — usage.

The linked‑compilation pattern is intentional in this codebase:

1. The helpers are `internal static` and meant to stay private to each consuming assembly.
   Promoting them to a shared assembly would force them to become `public`, which widens
   the public API surface for no functional gain.
2. `eShop.ServiceDefaults` is the canonical home for cross‑cutting *runtime* concerns (OTel
   wiring, health checks, service discovery defaults). The Shared helpers are
   *compile‑time-private build helpers* — different concern, different lifetime, different
   visibility. Folding them into ServiceDefaults would either (a) expose internals or
   (b) require `InternalsVisibleTo` on every consumer, both of which are worse than the
   status quo.
3. Deletion is not viable: removing the files breaks the build of every consumer listed below.

## Public type inventory

`src/Shared/` contains exactly two files. Public/visible types:

| File | Type | Visibility | Notes |
| --- | --- | --- | --- |
| `ActivityExtensions.cs` | `ActivityExtensions` (static class) | `internal` | Single extension method `SetExceptionTags(this Activity, Exception)` — sets OTel exception tags on an `Activity`. |
| `MigrateDbContextExtensions.cs` | `MigrateDbContextExtensions` (static class) | `internal` | Three `AddMigration<TContext>` overloads that register a hosted service to run `DbContext` migrations + seeding at startup, with OTel tracing. |
| `MigrateDbContextExtensions.cs` | `IDbSeeder<TContext>` (interface) | `public` | Seeder contract consumed by `MigrateDbContextExtensions.AddMigration<TContext, TDbSeeder>`. Public because it is implemented by seeder classes in consumer projects. |
| `MigrateDbContextExtensions.cs` | `MigrationHostedService<TContext>` (private nested class) | `private` | Implementation detail of `AddMigration`; not part of the public surface. |

Note: because these files are *linked* (compiled into each consumer assembly individually),
each consumer project gets its own copy of the `internal` types. There is no single shared
assembly exposing them.

## Consumer matrix

Determined by grepping `*.csproj` for `<Compile Include="..\Shared\` and grepping source for
direct symbol references (`SetExceptionTags`, `AddMigration`, `IDbSeeder`).

| Project | Links `ActivityExtensions.cs` | Links `MigrateDbContextExtensions.cs` | Symbol usage |
| --- | :---: | :---: | --- |
| `src/Catalog.API` | yes | yes | `AddMigration` (Extensions/Extensions.cs); `IDbSeeder<CatalogContext>` (Infrastructure/CatalogContextSeed.cs) |
| `src/Identity.API` | yes | yes | `AddMigration` (Program.cs); `IDbSeeder<ApplicationDbContext>` (UsersSeed.cs) |
| `src/Ordering.API` | yes | yes | `AddMigration` (Extensions/Extensions.cs); `IDbSeeder<OrderingContext>` (Infrastructure/OrderingContextSeed.cs) |
| `src/Webhooks.API` | yes | yes | `AddMigration` (Extensions/Extensions.cs) |
| `src/EventBusRabbitMQ` | yes | no | `SetExceptionTags` on `Activity` in `RabbitMQEventBus.cs` (publish/consume error tagging) |

`ActivityExtensions.cs` is linked by **5** projects.
`MigrateDbContextExtensions.cs` is linked by **4** projects.
Every API service that owns a relational database (`Catalog`, `Identity`, `Ordering`,
`Webhooks`) depends on `MigrateDbContextExtensions` for first‑run schema migration + seeding.

No project references `src/Shared` via `<ProjectReference>` (correct — there is no project to
reference).

## Solution file entry

`eShop.slnx` contains no entry for `src/Shared` (it is not a project). Therefore there is
nothing to remove from the solution file.

## Why not "consolidate into eShop.ServiceDefaults"?

Considered and rejected for this ticket:

- The helpers are `internal` by design. Moving them into ServiceDefaults forces a visibility
  change (`public`) or per‑consumer `InternalsVisibleTo` plumbing — both expand the public
  API surface of ServiceDefaults for no behavior change.
- ServiceDefaults today wires *runtime* infrastructure (OTel exporters, health endpoints,
  service discovery). `MigrateDbContextExtensions` is application‑startup glue specific to
  EF Core consumers; not every service that consumes ServiceDefaults wants an EF dependency.
- The linked‑compilation pattern is idiomatic in the upstream `dotnet/eShop` reference
  architecture this repo derives from. Rewriting it is a refactor with no measurable win.

If a future ticket decides consolidation is worthwhile (e.g., to enable unit testing the
helpers, or because a sixth consumer is added), the migration path would be:

1. Create a new `eShop.SharedKernel` (or similar) project under `src/`.
2. Move both files; promote `ActivityExtensions` to `public` (or add `InternalsVisibleTo`).
3. Replace each `<Compile Include="..\Shared\…">` with a `<ProjectReference>`.
4. Add the new project to `eShop.slnx`.
5. Delete `src/Shared/`.

That work is **explicitly out of scope** for TICKET-008. A follow‑up ticket should be filed
if/when the trigger condition above is met. As of this audit, no such trigger exists.

## Why not "delete"?

Deletion would break the build of all five consumer projects. Each of them has explicit
`<Compile Include="..\Shared\…">` entries and direct symbol usage (catalogued above). Per the
ticket's risk note, this was verified before recommending against deletion.

## Acceptance criteria coverage

- [x] `docs/audits/shared-library-decision.md` exists with a single explicit recommendation
      (KEEP) and rationale.
- [x] Every public/visible type in `src/Shared` is enumerated with its consumer list.
- [x] Recommendation is "keep" — no source files are modified in this ticket.
- [n/a] Deletion path: not executed; `dotnet build` verification not required.
