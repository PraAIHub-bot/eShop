# HybridApp Wiring Audit (TICKET-004)

**Date:** 2026-05-01
**Author:** dev-agent (TICKET-004 spike)
**Scope:** Read-only audit of `src/HybridApp` to decide whether to keep, archive, or delete the MAUI Blazor Hybrid client.
**Source findings:** 5× `unwired/HybridApp Frontend` (incl. "MAUI Blazor Hybrid").

---

## Recommendation: **KEEP** (do **not** archive or delete)

### One-paragraph justification

`src/HybridApp` is a live, fully-formed .NET MAUI Blazor Hybrid client with a real `MauiProgram.cs`, all five expected platform heads (Android, iOS, MacCatalyst, Windows, Tizen), and three working Razor pages (`Catalog`, `CatalogSearch`, `ItemPage`). It is included in the canonical solution `eShop.slnx` (line 20), it has a hard `<ProjectReference>` to `src/WebAppComponents/WebAppComponents.csproj`, and — most importantly — it talks to a backend that **is** orchestrated by Aspire: `MauiProgram.cs:9` hard-codes the `MobileBffHost` URL on port `11632`, which is exactly the YARP "mobile-bff" reverse proxy registered by `src/eShop.AppHost/Program.cs:60-62` and routed by `Extensions.cs#ConfigureMobileBffRoutes`. The "unwired" verdict is a static-graph false positive: the analyzer (a) cannot follow MAUI host registration (`UseMauiApp<App>`, `[MauiAsset]`, `BlazorWebView` → `RootComponent`), and (b) cannot recognise that the "missing" runtime edge from the AppHost to HybridApp is intentional — mobile clients are deployment leaves and they reach the platform through the mobile-bff YARP proxy at runtime, not through `builder.AddProject<Projects.HybridApp>(...)`. The conservative call is to keep the project. The audit does, however, surface one real, separately-fileable hygiene gap: there is **no** `pr-validation-maui.yml` build job for `src/HybridApp` (the existing MAUI workflow only builds `src/ClientApp`), so the project's compilability is not currently verified by CI on every PR. That should be addressed in a follow-up infra ticket — not by deleting the module.

---

## A. HybridApp project structure (does an active MAUI host exist?)

Answer: **yes**, every required piece of a working MAUI Blazor Hybrid app is present.

| Required artifact | Path | Status |
|---|---|---|
| MAUI host registration | `src/HybridApp/MauiProgram.cs` (35 lines) | Present — `MauiApp.CreateBuilder()`, `UseMauiApp<App>()`, font config, `AddMauiBlazorWebView()`, DI for `CatalogService` + `IProductImageUrlProvider` |
| MAUI `App` entry point | `src/HybridApp/App.xaml`, `App.xaml.cs` | Present — `Application` subclass with `CreateWindow → new Window(new MainPage())` |
| Main page hosting Blazor | `src/HybridApp/MainPage.xaml`, `MainPage.xaml.cs` | Present — `<BlazorWebView HostPage="wwwroot/index.html">` with `RootComponent` of `local:Routes` |
| Blazor router | `src/HybridApp/Components/Routes.razor` | Present — wraps a `<Router AppAssembly="@typeof(MauiProgram).Assembly">` with `MainLayout` |
| Layout + chrome | `Components/Layout/{MainLayout,HeaderBar,FooterBar}.razor` (+ `.razor.css`) | Present |
| Razor pages | `Components/Pages/Catalog/{Catalog.razor, CatalogSearch.razor}`, `Components/Pages/Item/ItemPage.razor` | Present (all three render `CatalogItem`/`CatalogResult` from WebAppComponents) |
| Blazor host page | `wwwroot/index.html` | Present — references `_framework/blazor.webview.js` and `HybridApp.styles.css` |
| Static assets | `wwwroot/css/`, `wwwroot/fonts/`, `wwwroot/icons/`, `wwwroot/images/`, `Resources/{AppIcon,Splash,Images,Fonts,Raw}/` | Present (full asset set: 19 web fonts, icons, splash SVG, app icon SVG, OpenSans TTF, etc.) |

### Platform heads

| Platform | Path | Sentinel files |
|---|---|---|
| Android | `src/HybridApp/Platforms/Android/` | `MainApplication.cs`, `MainActivity.cs`, `AndroidManifest.xml`, `Resources/values/colors.xml`, `Resources/xml/network_security_config.xml` |
| iOS | `src/HybridApp/Platforms/iOS/` | `AppDelegate.cs`, `Program.cs`, `Info.plist` |
| MacCatalyst | `src/HybridApp/Platforms/MacCatalyst/` | `AppDelegate.cs`, `Program.cs`, `Info.plist`, `Entitlements.{Debug,Release}.plist` |
| Windows | `src/HybridApp/Platforms/Windows/` | `App.xaml`, `App.xaml.cs`, `Package.appxmanifest`, `app.manifest` |
| Tizen | `src/HybridApp/Platforms/Tizen/` | `Main.cs`, `tizen-manifest.xml` (build-disabled by default in csproj — see §C.2) |

### `HybridApp.csproj` summary (key flags)

- `Sdk="Microsoft.NET.Sdk.Razor"`, `<UseMaui>true</UseMaui>`, `<SingleProject>true</SingleProject>`
- `<TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>`, plus `net10.0-windows10.0.19041.0` on Windows hosts
- `<OutputType>Exe</OutputType>`, `<RootNamespace>eShop.HybridApp</RootNamespace>`
- Package refs: `Microsoft.AspNetCore.Components.WebView.Maui` 9.0.30, `Microsoft.Maui.Controls` 9.0.30, `Microsoft.Maui.Controls.Compatibility` 9.0.30, `Microsoft.Extensions.Http` 9.0.0, `Microsoft.Extensions.Logging.Debug` 9.0.0
- `<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>` — HybridApp **opts out** of the repo-wide `Directory.Packages.props` and pins its own MAUI/Blazor package versions (intentional; required because MAUI tooling treats centralised versions inconsistently across workloads)
- One project reference: `..\..\src\WebAppComponents\WebAppComponents.csproj`
- **No `<Compile Include>` or `<Content Include>` shared-source links** — sharing is exclusively via the `WebAppComponents` project reference (verified by grep against the csproj: zero hits for `Compile Include`, `Content Include`, `Link=`)

---

## B. Shared file/component links between HybridApp and WebAppComponents / ClientApp

### B.1 Sharing with `WebAppComponents` — via project reference (the only mechanism)

**Mechanism:** `src/HybridApp/HybridApp.csproj:74` → `<ProjectReference Include="..\..\src\WebAppComponents\WebAppComponents.csproj" />`. There is no source-file linking (`<Compile Include … Link=`) — every shared element below is consumed through that one assembly reference, which is the idiomatic Razor Class Library pattern. WebAppComponents itself targets `net10.0` and declares `<SupportedPlatform Include="browser" />`, which is correct for a BlazorWebView host.

| # | Shared symbol | Source in WebAppComponents | Consumer in HybridApp | Type |
|---|---|---|---|---|
| 1 | `eShop.WebAppComponents.Catalog.CatalogItem` | `src/WebAppComponents/Catalog/CatalogItem.cs` | `Services/CatalogService.cs:3,13`, `Services/CatalogJsonContext.cs:6,8`, `Components/Pages/Item/ItemPage.razor:41,57`, `Components/Pages/Catalog/Catalog.razor:22` (via `<CatalogListItem Item="@item" />`) | DTO (record) |
| 2 | `eShop.WebAppComponents.Catalog.CatalogResult` | `src/WebAppComponents/Catalog/` (model) | `Services/CatalogService.cs:19,22,33`, `Services/CatalogJsonContext.cs:7`, `Components/Pages/Catalog/Catalog.razor:48,55` | DTO (record) |
| 3 | `eShop.WebAppComponents.Catalog.CatalogBrand` / `CatalogItemType` | `src/WebAppComponents/Catalog/` (models) | `Services/CatalogService.cs:40-51`, `Services/CatalogJsonContext.cs:9-12` | DTO (records) |
| 4 | `eShop.WebAppComponents.Services.ICatalogService` | `src/WebAppComponents/Services/` (interface) | `Services/CatalogService.cs:8` (`CatalogService : ICatalogService`) | Interface implemented by HybridApp |
| 5 | `eShop.WebAppComponents.Services.IProductImageUrlProvider` | `src/WebAppComponents/Services/` (interface) | `Services/ProductImageUrlProvider.cs:5` (impl), `MauiProgram.cs:31` (DI registration), `Components/Pages/Item/ItemPage.razor:6` (`@inject`) | Interface implemented by HybridApp |
| 6 | `eShop.WebAppComponents.Catalog.CatalogListItem` (Razor component) | `src/WebAppComponents/Catalog/CatalogListItem.razor` (+ `.razor.css`) | `Components/Pages/Catalog/Catalog.razor:22` `<CatalogListItem Item="@item" />` | Razor component (rendered) |
| 7 | `eShop.WebAppComponents.Item` namespace (root + sections) | `src/WebAppComponents/Item/` | `Components/_Imports.razor:12` (`@using eShop.WebAppComponents.Item`) | Razor `_Imports` global namespace |
| 8 | `eShop.WebAppComponents.Services` namespace | `src/WebAppComponents/Services/` | `Components/_Imports.razor:11` | Razor `_Imports` global namespace |
| 9 | `eShop.WebAppComponents.Catalog` namespace | `src/WebAppComponents/Catalog/` | `Components/_Imports.razor:13` | Razor `_Imports` global namespace |

**Note on `CatalogSearch`:** WebAppComponents has its own `Catalog/CatalogSearch.razor`. HybridApp ships a local copy at `Components/Pages/Catalog/CatalogSearch.razor` and explicitly disambiguates by writing `<eShop.HybridApp.Components.Pages.Catalog.CatalogSearch …>` in `Catalog.razor:10`. That is intentional shadowing (mobile vs web filter UX), **not** a duplication bug.

### B.2 Sharing with `src/ClientApp` (the older MAUI client)

**None.** `grep -rn "ClientApp" src/HybridApp` and `grep -rn "HybridApp" src/ClientApp` both return **zero** matches. The two MAUI clients do not share code, types, or namespaces. (Confirmed independently in the prior `docs/audits/clientapp-removal-audit.md` §C.4.)

### B.3 Aspire AppHost ↔ HybridApp wiring (the question that produced 5 "unwired" findings)

This is the crux of the unwired verdict. There is **no** `builder.AddProject<Projects.HybridApp>(...)` line in `src/eShop.AppHost/Program.cs`, and there is **no** `<ProjectReference>` to `HybridApp.csproj` from `src/eShop.AppHost/eShop.AppHost.csproj`. A graph-based analyzer therefore concludes the project has no incoming edge.

**That conclusion is wrong because the runtime contract is over HTTP, not over Aspire orchestration.** The actual wiring:

1. `src/eShop.AppHost/Program.cs:60-62` registers a YARP reverse proxy named `"mobile-bff"`:
   ```csharp
   builder.AddYarp("mobile-bff")
       .WithExternalHttpEndpoints()
       .ConfigureMobileBffRoutes(catalogApi, orderingApi, identityApi);
   ```
2. `src/eShop.AppHost/Extensions.cs:185+` (`ConfigureMobileBffRoutes`) wires routes onto the `catalog-api`, `ordering-api`, and `identity-api` clusters specifically for the catalog/ordering surface a mobile client consumes.
3. `src/HybridApp/MauiProgram.cs:9` hard-codes the URL the mobile client uses to reach that proxy:
   ```csharp
   internal static string MobileBffHost =
       DeviceInfo.Platform == DevicePlatform.Android
           ? "http://10.0.2.2:11632/"   // Android emulator → host loopback
           : "http://localhost:11632/";  // iOS sim, MacCatalyst, Windows
   ```
4. The same URL is used by `Services/ProductImageUrlProvider.cs:8` for product image lookups and by `Services/CatalogService.cs` for catalog calls.
5. `src/ClientApp/ViewModels/SettingsViewModel.cs:58,68` independently hard-codes the same `:11632` port — i.e., **both** MAUI clients (HybridApp and ClientApp) are intended consumers of the same `mobile-bff` YARP, which is the documented integration point.

So the AppHost **is** wired to the mobile clients; the wiring is "AppHost exposes a mobile-bff YARP at 11632, and the mobile clients are configured at compile time to call that port." A static analyzer that only follows `<ProjectReference>` and `builder.AddProject<...>(...)` edges cannot see this — but it is the correct architecture for an Aspire-orchestrated backend with externally-deployed mobile clients.

---

## C. Does `dotnet build src/HybridApp/HybridApp.csproj` currently succeed?

**Cannot be executed in this audit sandbox** — no `dotnet` SDK is installed (`command -v dotnet` → not found) and the MAUI cross-platform workloads (`android`, `ios`, `maccatalyst`, `maui`) cannot be installed here even if the SDK were present. Building `HybridApp.csproj` requires, at minimum, the .NET 10 SDK (per `global.json`: `"sdk.version": "10.0.100"`) **plus** the MAUI workloads (`dotnet workload install android ios maccatalyst maui`) — and a Windows host is additionally required to build the `net10.0-windows10.0.19041.0` target. So the canonical answer to "does it currently build?" cannot be produced from this Linux sandbox; it must be answered by a Windows CI runner with MAUI workloads installed.

### C.1 Static buildability check — no obvious blockers found

Although a real `dotnet build` was not executed, I checked the structural prerequisites that would cause a build to fail at parse / restore / reference-resolution time:

| Check | Result |
|---|---|
| `WebAppComponents.csproj` exists at the path the project reference points to | ✅ `src/WebAppComponents/WebAppComponents.csproj` (19 lines) |
| `WebAppComponents` target framework is reachable from MAUI net10.0-* heads | ✅ `net10.0` (compatible — Razor Class Library with `<SupportedPlatform Include="browser" />`) |
| All shared types referenced by HybridApp exist in WebAppComponents | ✅ `CatalogItem`, `CatalogResult`, `CatalogBrand`, `CatalogItemType`, `ICatalogService`, `IProductImageUrlProvider`, `CatalogListItem.razor` all present (see §B.1) |
| `MauiProgram.cs` symbols (`MauiApp`, `BlazorWebViewDeveloperTools`, `AddMauiBlazorWebView`, `DeviceInfo`) are provided by referenced packages | ✅ `Microsoft.Maui.Controls` 9.0.30 + `Microsoft.AspNetCore.Components.WebView.Maui` 9.0.30 |
| Platform-head sentinel files compile-clean (no obviously broken stubs) | ✅ Spot-check of `Platforms/{Android,iOS,Windows,MacCatalyst}/*.cs` shows generated MAUI heads that match the templates |
| Repo `global.json` is coherent | ✅ `sdk.version: 10.0.100`, `rollForward: latestFeature`, `allowPrerelease: true`, `MSTest.Sdk: 4.0.2` |

There is **no obvious reason** the build would fail. The risk is operational (workload install on the build agent), not structural.

### C.2 Notable build-time caveats

1. **Tizen target framework is commented out** in the csproj (line 7) — Tizen platform-head files exist on disk (`Platforms/Tizen/Main.cs`, `tizen-manifest.xml`) but are **not** part of the build by default. Anyone uncommenting that line will need to install the Tizen workload separately. Not a current build blocker.
2. **Centralised package management is disabled** for HybridApp (`<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>`). HybridApp's package versions are pinned in its csproj and are **not** governed by the repo's `Directory.Packages.props`. This is intentional (MAUI workloads do not always cooperate with central versioning) and is a documented MAUI guidance, but it means dependency upgrades for HybridApp must be done manually on this csproj.
3. **No xUnit/MSTest test project exists** for HybridApp (`tests/**HybridApp**` glob → empty). This matches the `test_quality/HybridApp` finding listed in the source analysis (`No tests for module 'HybridApp'`). Recommendation: handle this in the broader test-coverage ticket (TICKET-011 already added xUnit scaffolding for backend modules), not in this spike.
4. **No CI workflow currently builds HybridApp.** `.github/workflows/pr-validation-maui.yml` exists but its `paths:` filter and `dotnet build` step both target `src/ClientApp/ClientApp.csproj` only — there is no parallel job for `src/HybridApp/HybridApp.csproj`. This is the single most consequential gap surfaced by this audit (see §D).

---

## D. Findings observed during the audit

1. **Five "unwired" findings against `src/HybridApp` are false positives at the module level.** Reason: the HybridApp ↔ Aspire integration is over HTTP via the `mobile-bff` YARP proxy (port 11632), not via `<ProjectReference>` or `builder.AddProject<...>(...)`. A static analyzer cannot follow this edge. ClientApp produces the same false positive for the same reason and was previously kept (see `docs/audits/clientapp-removal-audit.md` §C.1).
2. **HybridApp is NOT listed in `eShop.Web.slnf`** (the Windows-Visual-Studio "web only" solution filter). It is in the canonical `eShop.slnx` (line 20). This is correct — the `.slnf` is intentionally web-only to keep VS startup fast, and a MAUI project would force a workload prerequisite onto every backend developer. Not a bug.
3. **No CI build for HybridApp.** `pr-validation-maui.yml` only runs against `src/ClientApp`. HybridApp.csproj is therefore not regularly verified by automation. This is the single most actionable gap from this audit.
4. **No tests for HybridApp** (already separately tracked under `test_quality/HybridApp`).
5. **Two MAUI clients (HybridApp + ClientApp) coexist and both target the same mobile-bff.** They do not share source. If maintainers later decide to retire one in favour of the other, both audits (this one and `clientapp-removal-audit.md`) should be re-read together — but that is **out of scope** for this spike.
6. **Hard-coded port `11632`** appears in three places: `src/HybridApp/MauiProgram.cs:9`, `src/HybridApp/Services/ProductImageUrlProvider.cs:8` (transitively, via `MauiProgram.MobileBffHost`), and `src/ClientApp/ViewModels/SettingsViewModel.cs:58,68`. The AppHost itself uses `builder.AddYarp("mobile-bff").WithExternalHttpEndpoints()` and does **not** set a fixed port. There is therefore an implicit contract — "the mobile-bff is reachable at host port 11632" — that is not codified in the AppHost. A latent runtime risk if Aspire ever assigns a different host port for that resource. Not a build blocker; flag-only.
7. **Implementation Notes obeyed.** No HybridApp source files were modified by this ticket. The only file written is this audit (`docs/audits/hybridapp-status.md`). Verified with `git status` before and after.

---

## E. Follow-up tickets (only the ones this audit identified)

Since the recommendation is **KEEP**, no removal ticket should be opened on the basis of this audit. The audit did, however, surface **two** small, fileable hygiene tickets:

### E.1 (recommended) `infra: add HybridApp to pr-validation-maui CI workflow`

**Type:** infra · **Priority:** medium

**Scope:**
1. Edit `.github/workflows/pr-validation-maui.yml`:
   - Add `'src/HybridApp/**'` to the `paths:` filter on both `pull_request` and `push` triggers.
   - Add a second build step (or a matrix entry) that runs `dotnet build src/HybridApp/HybridApp.csproj` after the existing ClientApp build, on the same `windows-latest` runner with the same MAUI workload install.
2. Out of scope: any change to `src/HybridApp` source. Out of scope: adding tests (see TICKET-011 / module test-coverage ticket).
3. Acceptance: a PR that touches `src/HybridApp/**` triggers the workflow and the workflow's HybridApp build step succeeds on `windows-latest`.

### E.2 (optional, defer-able) `chore: pin mobile-bff host port in AppHost`

**Type:** tech-debt · **Priority:** low

**Scope:** Codify the implicit `11632` contract by giving `builder.AddYarp("mobile-bff")` an explicit port binding (e.g. `.WithEndpoint(port: 11632, name: "mobile-bff", scheme: "http")`) so that the AppHost is the single source of truth and both MAUI clients can be validated against it at runtime. Out of scope: any change to MAUI client source.

(E.2 is a "nice to have" — the audit did not surface evidence of an actual port collision, only a latent risk, and this would be a small refactor on the AppHost rather than a HybridApp change.)

---

## F. Audit method (reproducibility)

Tool-equivalent commands used to produce this report. All read-only.

```sh
# Enumerate every file in HybridApp
find src/HybridApp -type f                               # → 80 files (all listed §A)

# Confirm a real MauiProgram.cs and platform heads
ls src/HybridApp/MauiProgram.cs src/HybridApp/App.xaml*  src/HybridApp/MainPage.xaml*
ls src/HybridApp/Platforms/{Android,iOS,MacCatalyst,Windows,Tizen}

# Solution membership
grep -n "HybridApp" eShop.slnx                           # → line 20 (HybridApp.csproj)
grep -n "HybridApp" eShop.Web.slnf                       # → no matches (web-only filter, by design)

# Aspire AppHost wiring
grep -n "HybridApp" src/eShop.AppHost/Program.cs                # → no matches (false-positive driver)
grep -n "HybridApp" src/eShop.AppHost/eShop.AppHost.csproj      # → no matches (likewise)
grep -En "11632|mobile-bff|MobileBff" src                       # → 3 hits in HybridApp, 2 in AppHost, 2 in ClientApp

# All cross-references between HybridApp and the rest of the repo
grep -rln "HybridApp" .                                  # → 28 files (listed in §A/§B)
grep -rln "ClientApp" src/HybridApp                      # → no matches (no sharing with ClientApp)
grep -rln "HybridApp" src/ClientApp                      # → no matches (no sharing the other way)

# csproj sanity — no source-file linking, only a project reference
grep -nE "Compile Include|Content Include|Link=" src/HybridApp/HybridApp.csproj   # → no matches
grep -n "ProjectReference" src/HybridApp/HybridApp.csproj                          # → 1 match (WebAppComponents)

# CI coverage gap
grep -n "HybridApp" .github/workflows/pr-validation-maui.yml   # → no matches (gap, see §D.3 / §E.1)

# README / docs mention
grep -n "HybridApp" README.md                            # → no matches (HybridApp is not mentioned in README;
                                                         #   only "Optional: Install [.NET MAUI Workload]" appears,
                                                         #   which is a generic MAUI prerequisite, not a HybridApp run instruction)

# Build attempt (sandbox limitation, see §C)
command -v dotnet                                        # → not found in this sandbox; build cannot be executed here
```

**No source files outside `docs/audits/` were modified by this ticket.** Verified by running `git status` immediately before commit.
