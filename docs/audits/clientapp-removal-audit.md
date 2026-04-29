# ClientApp Removal Audit (TICKET-001)

**Date:** 2026-04-29
**Author:** dev-agent (TICKET-001 spike)
**Scope:** Read-only audit of `src/ClientApp` to determine whether the module can be safely deleted.
**Source findings:** `dead_code/ClientApp` (entire module, 198 files); `unwired/ClientApp Frontend` (multiple).

---

## Recommendation: **KEEP** (do **not** delete)

### One-paragraph justification

`src/ClientApp` is a .NET MAUI mobile/desktop client app (iOS, Android, MacCatalyst, Windows) — it is intentionally a leaf project with no incoming runtime references, which is exactly the shape a static dead-code analyzer mis-classifies as "dead." Concrete evidence that the module is alive: it is still listed in the canonical solution file `eShop.slnx` (line 21), it has a dedicated test project `tests/ClientApp.UnitTests` (also in `eShop.slnx`, line 28) with eight active test files, and it is gated by **three** CI workflows (`pr-validation.yml`, `pr-validation-maui.yml`, `playwright.yml`) — `pr-validation-maui.yml` actively runs `dotnet build src/ClientApp/ClientApp.csproj` and `dotnet test ... ClientApp.UnitTests.csproj` on every PR that touches those paths. The original "dead code" verdict is correct only in the narrow sense that the Aspire AppHost does not reference it (mobile clients are deployed independently and not orchestrated by Aspire), but that does not make the project dead. Deleting it would also delete the maintained test suite and break (or render no-op) all three CI workflows. The conservative call is to keep it; if the maintainers have separately decided to retire ClientApp in favor of `src/HybridApp` (the newer MAUI Blazor Hybrid client), that decision should be confirmed by a human owner before any deletion ticket is opened.

---

## A. References found ("every grep hit for `ClientApp`")

Search command equivalent: `grep -rn "ClientApp" .` (full repo). Total: **183 files** contain the string `ClientApp`. Of those, 161 live inside `src/ClientApp/**` itself and 16 live inside `tests/ClientApp.UnitTests/**` — those are intra-module / dedicated-test references and are listed in summary form below. The remaining **6 external references** are listed in full.

### A.1 External references (outside `src/ClientApp/**` and `tests/ClientApp.UnitTests/**`)

These are the references that would need to be updated or removed if the module were deleted:

| # | File | Line(s) | What it does |
|---|---|---|---|
| 1 | `eShop.slnx` | 21 | `<Project Path="src/ClientApp/ClientApp.csproj" />` — adds ClientApp to the root solution |
| 2 | `eShop.slnx` | 28 | `<Project Path="tests/ClientApp.UnitTests/ClientApp.UnitTests.csproj" />` — adds the unit test project to the root solution |
| 3 | `.github/workflows/pr-validation-maui.yml` | 8–9, 15–16, 39, 42 | Path filter (`src/ClientApp/**`, `tests/ClientApp.UnitTests/**`); `dotnet build src/ClientApp/ClientApp.csproj`; `dotnet test --project tests/ClientApp.UnitTests/ClientApp.UnitTests.csproj` |
| 4 | `.github/workflows/pr-validation.yml` | 7–8, 15–16 | `paths-ignore` filter on `src/ClientApp/**` and `tests/ClientApp.UnitTests/**` (skips this workflow when only ClientApp changes) |
| 5 | `.github/workflows/playwright.yml` | 7–8, 14–15 | `paths-ignore` filter on `src/ClientApp/**` and `tests/ClientApp.UnitTests/**` (also `test/ClientApp.UnitTests/**` — note the typo, see "Findings" §C) |
| 6 | `tests/ClientApp.UnitTests/ClientApp.UnitTests.csproj` | 22 | `<ProjectReference Include="..\..\src\ClientApp\ClientApp.csproj" />` |

### A.2 Internal references (summary)

- **`src/ClientApp/**` — 161 files** that match `ClientApp`. These are namespace declarations (`namespace eShop.ClientApp...`), `using eShop.ClientApp.*;` directives, and XAML `x:Class="eShop.ClientApp..."` / `clr-namespace:eShop.ClientApp...` attributes. Representative roots: `App.xaml.cs`, `AppShell.xaml.cs`, `MauiProgram.cs`, `ClientApp.csproj`, `ClientApp.sln`, plus subtrees `Animations/`, `Controls/`, `Converters/`, `Effects/`, `Exceptions/`, `Extensions/`, `Helpers/`, `Messages/`, `Models/`, `Platforms/{Android,iOS,MacCatalyst,Windows}`, `Resources/`, `Services/{AppEnvironment,Basket,Catalog,Common,Dialog,FixUri,Identity,Location,Navigation,OpenUrl,Order,RequestProvider,Settings,Theme}`, `Triggers/`, `Validations/`, `ViewModels/`, `Views/`. The total file count under `src/ClientApp/` is **198 files** (confirms the analyzer's 198-file finding; the 161 figure is the subset that literally contains the string `ClientApp` — the remaining 37 files are images, fonts, JSON, and other resources that do not mention the name in their bytes).
- **`tests/ClientApp.UnitTests/**` — 16 files** that match `ClientApp`. Includes the project file, `.sln`, `GlobalUsings.cs`, `TestingExtensions.cs`, `Mocks/Mock{Dialog,Navigation,Settings,ViewModel}Service.cs`, `Services/{Basket,Catalog,Orders}ServiceTests.cs`, `ViewModels/{Catalog,CatalogItem,Main,Mock,Order}ViewModelTests.cs`. All reference types from `eShop.ClientApp.*` and use the `ClientApp.UnitTests` namespace.

### A.3 Other files containing the string

- `CLAUDE.md` (this ticket's instructions) — not a code reference.
- `TICKET-CONTEXT.yaml` (this ticket's context) — not a code reference.

---

## B. Required-by-criteria reference list (project-references, sln entries, docker-compose entries)

### B.1 Project references (`.csproj` `<ProjectReference>` to ClientApp)

Searched: every `.csproj` in the repo. Only **one** project references `ClientApp.csproj`:

- `tests/ClientApp.UnitTests/ClientApp.UnitTests.csproj`:22 → `<ProjectReference Include="..\..\src\ClientApp\ClientApp.csproj" />`

**Critically, `src/eShop.AppHost/eShop.AppHost.csproj` does NOT reference ClientApp.** Verified: the AppHost csproj's full `<ProjectReference>` list is `Basket.API`, `Catalog.API`, `Identity.API`, `Ordering.API`, `OrderProcessor`, `PaymentProcessor`, `Webhooks.API`, `WebApp`, `WebhookClient` — no ClientApp.

Likewise, `src/eShop.AppHost/Program.cs` contains no `ClientApp` token and no `builder.AddProject<Projects.ClientApp>(...)` line. Aspire orchestration does not run ClientApp at runtime.

### B.2 Solution entries

The repository uses **`eShop.slnx`** (the new XML-based solution format); there is **no `eShop.sln`** at the root (only `src/ClientApp/ClientApp.sln`, an internal MAUI solution, and `tests/ClientApp.UnitTests/ClientApp.UnitTests.sln`).

`eShop.slnx` entries that mention ClientApp:

- Line 21: `<Project Path="src/ClientApp/ClientApp.csproj" />`
- Line 28: `<Project Path="tests/ClientApp.UnitTests/ClientApp.UnitTests.csproj" />`

### B.3 Docker-compose entries

**None.** The repository contains no `docker-compose*.yml` files at any depth (verified with `find . -name 'docker-compose*'` — empty result). Container orchestration in this repo is handled by the .NET Aspire AppHost, not Compose.

### B.4 Other build/CI entries

- `.github/workflows/pr-validation-maui.yml` — dedicated MAUI build/test workflow, runs on changes to `src/ClientApp/**` or `tests/ClientApp.UnitTests/**`. Builds `src/ClientApp/ClientApp.csproj` and tests `tests/ClientApp.UnitTests/ClientApp.UnitTests.csproj`.
- `.github/workflows/pr-validation.yml` — main backend PR-validation workflow; explicitly *ignores* changes confined to ClientApp via `paths-ignore`.
- `.github/workflows/playwright.yml` — Playwright e2e workflow; also *ignores* ClientApp-only changes via `paths-ignore`.

---

## C. Notable findings observed during the audit

1. **The "dead code" verdict is a false positive at the module level.** A static analyzer that flags `src/ClientApp` as dead because nothing references it is correctly observing graph topology but mis-applying the conclusion. Mobile clients are deployment leaves; they have no incoming references by design.
2. **`playwright.yml` has a typo** (line 15): `'test/ClientApp.UnitTests/**'` (singular `test/`) where every other workflow uses `tests/` (plural). This means the `paths-ignore` rule on that line is effectively dead — the directory `test/` does not exist. Worth a small follow-up fix, but unrelated to deletion.
3. **No README mention.** The root `README.md` does not mention ClientApp. If maintainers do consider ClientApp the "official mobile client," that is undocumented.
4. **HybridApp coexists.** `src/HybridApp` (a MAUI Blazor Hybrid app, also flagged unwired) appears to be a newer client that may overlap with ClientApp. The two do **not** reference each other (verified: `grep -rn "ClientApp" src/HybridApp` → no matches). If the team has decided HybridApp supersedes ClientApp, that decision is the only thing that would justify deletion.
5. **`eShop.AppHost` is clean.** No runtime risk: confirmed Aspire does not host or reference ClientApp, so the ticket's stated risk ("ClientApp may be referenced by Aspire AppHost at runtime even if not compiled into other projects") is **disproved** by this audit.

---

## D. Follow-up ticket scope (only if "delete" is chosen by a human owner)

Since the recommendation is **KEEP**, no removal ticket should be opened on the basis of this audit alone. **However**, if a human maintainer separately confirms that ClientApp is retired in favor of HybridApp, a single follow-up ticket should be filed with the following exact scope:

**Title:** `chore: remove retired ClientApp MAUI module`
**Type:** tech-debt
**Priority:** medium
**Pre-condition:** explicit sign-off (issue comment or commit) from a code owner of `src/ClientApp/` confirming the module is retired.

**Steps:**
1. Delete directories: `src/ClientApp/` (198 files) and `tests/ClientApp.UnitTests/` (~30 files).
2. Edit `eShop.slnx`: remove lines 21 and 28 (the two `<Project Path="...ClientApp..." />` entries).
3. Delete `.github/workflows/pr-validation-maui.yml` entirely (its only purpose is building ClientApp).
4. Edit `.github/workflows/pr-validation.yml`: remove the two `'src/ClientApp/**'` and `'tests/ClientApp.UnitTests/**'` lines from the `paths-ignore` blocks (no longer meaningful once the paths are gone).
5. Edit `.github/workflows/playwright.yml`: same as step 4, plus fix or remove the `'test/ClientApp.UnitTests/**'` typo line.
6. Verify `dotnet build eShop.slnx` and `dotnet test` both succeed with no ClientApp-related errors or warnings.
7. Verify CI is green on the resulting PR.

**Out of scope for the removal ticket:**
- Any change to `src/HybridApp` or `src/WebApp`.
- Any change to `src/eShop.AppHost/` (already does not reference ClientApp).
- README updates (ClientApp is not mentioned there).

**Acceptance criteria for the removal ticket:**
- `grep -rn "ClientApp" .` returns matches only in (a) historical commit messages, (b) this audit document, (c) `tests/Ordering.*`/`tests/Catalog.*` only if false-positive substring hits exist (none currently expected).
- `dotnet build eShop.slnx` succeeds.
- All remaining CI workflows run green on a no-op PR.
- This audit document is updated with a closing note linking the removal PR.

---

## E. Audit method (reproducibility)

Commands used (or their tool-equivalents) to produce this report:

```sh
# Enumerate every reference to "ClientApp" in the repo
grep -rn "ClientApp" .

# Confirm 198-file count under src/ClientApp
find src/ClientApp -type f | wc -l   # → 198

# Confirm Aspire AppHost has no reference
grep -n "ClientApp" src/eShop.AppHost/Program.cs            # → no matches
grep -n "ClientApp" src/eShop.AppHost/eShop.AppHost.csproj  # → no matches

# Confirm no docker-compose files exist
find . -name 'docker-compose*'                              # → empty

# Confirm README has no mention
grep -n "ClientApp" README.md                               # → no matches

# Solution entries
grep -n "ClientApp" eShop.slnx                              # → lines 21, 28
```

No source files outside `docs/audits/` were modified by this ticket.
