# MissionPlanner Map Runtime Integration & Hardening — Codex Task Set

## Purpose

The map refactor has produced a strong infrastructure foundation, but the live MissionPlanner map still does not consistently use that infrastructure end-to-end. The goal of this task set is to finish composition and hardening without adding more map features and without reversing ADR-0006.

The target runtime flow is:

```text
PlannerSettings.Map.SelectedSourceId
                 │
                 ▼
          IMapSourceResolver
         ┌───────┼─────────┐
         │       │         │
      Catalog   Pack     Custom
         └───────┼─────────┘
                 ▼
         ResolvedMapSource
       ┌─────────────────────┐
       │ provider/product    │
       │ source kind         │
       │ effective policy    │
       │ credential state    │
       │ attribution         │
       │ endpoint/archive    │
       └─────────────────────┘
                 │
                 ▼
     CompositeMapsuiBasemapFactory
       ├─ built-in raster
       ├─ hosted raster
       ├─ custom raster/WMS/WMTS
       └─ raster MBTiles
                 │
                 ▼
         MapBasemapController
      UI-thread + last-write-wins
                 │
        ┌────────┴─────────┐
        ▼                  ▼
 Attribution          Map HTTP
 Coordinator          fetch/cache
        │              pipeline
        ▼
 visible overlay
```

Operational mission layers stay outside basemap replacement.

## Global repository constraints

- Work only in the new MissionPlanner implementation under `src/`, `docs/`, `scripts/`, and test-data folders.
- Treat `src-v.1.38/` as read-only reference material. Never modify, format, move, or include legacy files in a commit.
- Preserve ADR-0006: **PMTiles/vector support is deferred, not rejected**.
- Do not implement production PMTiles/vector rendering in this task set.
- Keep Mapsui/BruTile as the current production renderer.
- Keep Mapsui types out of `MissionPlanner.Core` and out of platform-neutral map catalog/policy models.
- Preserve the basemap/operational-layer split. Mission, vehicle, waypoint, track, fence, rally, ADS-B, POI and similar layers must survive a basemap change.
- `PlannerSettings.Map.SelectedSourceId` becomes the single runtime source of truth by the end of this task set.
- Provider credentials/tokens must never be committed, written to ordinary settings, printed to logs, exported in diagnostics, or included in pack manifests.
- All HTTP/file/network work must be cancellation-aware and bounded.
- Provider-policy checks must be enforced in the runtime path, not merely represented as metadata.
- Offline packs and HTTP cache must remain physically and logically separate.
- Do not mutate UI-bound observable collections from `Dispose()`.
- Add deterministic tests in each task.
- Keep manual platform verification separate from deterministic CI.
- Commit after each task.

---

# Task 01 — Make `SelectedSourceId` authoritative

## Objective

Remove the split runtime between legacy `PlannerMapProvider` / `PlannerMapStyle` / `SelectedMapType` and the new catalog-driven `PlannerSettings.Map.SelectedSourceId`.

At completion, every basemap selection originates from `SelectedSourceId`.

## Current code to inspect first

Review the current implementations around:

```text
PlannerTabViewModel
MissionItemListViewModel
MissionMapPresenter
Planner settings model/version migration
BuiltInMapSourceIds
PlannerMapProvider
PlannerMapStyle
SelectedMapType
```

Confirm the exact current behavior before changing code.

## Required design

Introduce a platform-neutral source resolver:

```csharp
public interface IMapSourceResolver
{
    ValueTask<MapSourceResolutionResult> ResolveAsync(
        string sourceId,
        CancellationToken cancellationToken = default);
}
```

Use a typed result instead of exceptions for ordinary configuration states:

```text
None
UnknownSource
Disabled
Deferred
CredentialMissing
PackMissing
CustomSourceMissing
PolicyDenied
InvalidDefinition
UnsupportedByRenderer
Cancelled
```

Create a `ResolvedMapSource` containing enough information for the renderer layer without leaking Mapsui:

```text
Id
Origin
Provider
DataProduct
MapSourceDefinition
EffectiveMapPolicy
Attribution
Credential requirement/configured state
Location or endpoint/archive reference
Tile content format
Archive format
Access kind
```

## Supported source namespaces

Resolver must support at least:

```text
built-in/catalog:
    osm-standard
    esri-world-topo
    esri-world-physical
    esri-shaded-relief
    esri-dark-gray
    no-map

installed pack:
    pack:<pack-id>:<version>

custom source:
    custom:<source-id>

hosted providers:
    source IDs from the catalog
```

Do not infer provider behavior from arbitrary ID prefixes once provider/product relationships exist in the catalog.

## Legacy settings migration

Add an explicit migration from the old provider/style representation.

Required mappings include:

```text
OpenStreetMap + Standard -> osm-standard
Esri + Topographic       -> esri-world-topo
Esri + Physical          -> esri-world-physical
Esri + ShadedRelief      -> esri-shaded-relief
Esri + DarkGray          -> esri-dark-gray
NoMap                    -> no-map
```

Rules:

1. Run this mapping only when loading an older settings schema where `SelectedSourceId` is absent/unset.
2. Never overwrite an already valid modern `SelectedSourceId`.
3. Unknown legacy values fall back deterministically and emit a migration warning.
4. Preserve the user's existing selected map wherever possible.
5. Bump schema only if required by the current settings versioning mechanism.

## Runtime cleanup

By the end of this task:

- `SelectedSourceId` is the only writable source-of-truth for basemap selection.
- Legacy provider/style fields may remain for backward deserialization only.
- No presenter/controller resolves a source from provider/style values during normal runtime.
- Avoid two-way synchronization between the old and new models.

## Tests

Add deterministic tests for:

- every built-in source;
- installed pack resolution;
- missing pack;
- custom source resolution;
- missing custom source;
- credential-required hosted source;
- disabled/deferred source;
- renderer-unsupported source;
- all legacy migration mappings;
- modern value not overwritten;
- unknown source fallback;
- cancellation.

## Documentation

Update:

```text
docs/MAPS.md
docs/PLANNER_SETTINGS.md
docs/FEATURES.md
```

State explicitly that `PlannerSettings.Map.SelectedSourceId` is the authoritative runtime basemap selection.

## Acceptance criteria

- No production runtime map selection depends on legacy provider/style fields.
- Pack/custom/hosted source IDs resolve through the same abstraction.
- Legacy settings preserve previous user choices.

---

# Task 02 — Compose all raster source types into one Mapsui runtime path

## Objective

Make built-in, hosted, custom and raster-MBTiles implementations reachable through one production `IMapsuiBasemapFactory` and register the complete runtime graph in DI.

## Dependency

Task 01.

## Current code to inspect

```text
MapsuiBasemapFactory
MapsuiHostedBasemapFactory
MapsuiMbTilesSourceFactory
custom Mapsui source implementation
MapBasemapController
MissionMapPresenter
ApplicationConfigurator
```

Verify which factories are currently production-referenced versus test-only/orphaned.

## Required design

Expose one runtime renderer interface:

```csharp
public interface IMapsuiBasemapFactory
{
    ValueTask<MapBasemapCreationResult> CreateAsync(
        ResolvedMapSource source,
        CancellationToken cancellationToken = default);
}
```

Implement an internal dispatch/composite factory such as:

```text
CompositeMapsuiBasemapFactory
    ├─ BuiltInRasterMapsuiBasemapFactory
    ├─ HostedRasterMapsuiBasemapFactory
    ├─ CustomRasterMapsuiBasemapFactory
    ├─ WmsWmtsMapsuiBasemapFactory
    └─ MbTilesMapsuiBasemapFactory
```

Only create sub-factories corresponding to source kinds already supported by the codebase. Do not add speculative vector factories.

Dispatch by typed properties:

```text
AccessKind
ArchiveFormat
TileContentFormat
Origin
```

not provider-name string matching.

## Creation result

Use typed outcomes for ordinary failures:

```text
Success
Unsupported
PolicyDenied
CredentialMissing
SourceUnavailable
InvalidConfiguration
RendererFailure
Cancelled
```

Do not throw for expected configuration states.

## Live map integration

The runtime path becomes:

```text
SelectedSourceId changed
    -> IMapSourceResolver.ResolveAsync
    -> IMapsuiBasemapFactory.CreateAsync
    -> MapBasemapController.TrySwitchAsync
```

Requirements:

- old basemap remains active if resolution/creation fails;
- UI receives a concise useful failure state;
- presenter does not contain provider/source-kind branching.

## DI composition

Register the complete production graph with deliberate lifetimes:

```text
IMapCatalog
IMapPolicyEvaluator
IMapSourceResolver
IMapSecretStore / IMapCredentialStore
IOfflineMapPackRepository
IOfflineMapPackValidator
IOfflineMapPackInstaller
custom-source store/service
hosted-source service
IMapsuiBasemapFactory and internal sub-factories
map attribution services needed by source resolution
```

Avoid service-locator usage from views.

## MBTiles end-to-end requirement

Selecting `pack:<id>:<version>` must:

1. resolve installed pack;
2. verify archive still exists/is valid enough to open;
3. open MBTiles read-only;
4. create Mapsui basemap;
5. replace only basemap layer;
6. work with network disabled.

## Hosted/custom requirement

A source becomes renderable only when:

```text
definition valid
policy permits interactive use
credential configured if required
current production renderer supports the source type
```

Vector candidates remain unsupported/deferred.

## Tests

Add tests for:

- source resolver + composite routing;
- every built-in source;
- offline MBTiles source;
- hosted raster source;
- custom XYZ;
- WMS/WMTS if implementation claims support;
- missing credential;
- unsupported vector source;
- factory failure leaves old basemap intact;
- DI resolves production runtime graph;
- platform-neutral map project has no Mapsui reference.

## Documentation

Update `docs/MAPS.md` and `docs/FEATURES.md` to list which source kinds are actually runtime-integrated.

## Acceptance criteria

- Selecting a supported `SelectedSourceId` changes the live mission map.
- Offline MBTiles is genuinely selectable.
- Hosted/custom/MBTiles factories are no longer orphaned.

---

# Task 03 — Build the real map HTTP/cache/policy pipeline

## Objective

Make the existing HTTP/cache/policy primitives control actual hosted/custom raster requests and make Planner cache settings effective.

## Dependency

Tasks 01–02.

## Current code to inspect

```text
IMapHttpClientFactory
MapHttpOptions
MapHttpDiskCache
MapPolicyEvaluator
MapsuiHostedBasemapFactory
OSM/Esri source creation
Planner cache settings
ApplicationConfigurator
```

Confirm whether any current Mapsui/BruTile convenience source bypasses the central network path.

## Introduce a cohesive fetcher

Prefer one runtime abstraction:

```csharp
public interface IMapHttpResourceFetcher
{
    ValueTask<MapHttpFetchResult> FetchAsync(
        MapHttpFetchRequest request,
        CancellationToken cancellationToken = default);
}
```

Request contains:

```text
resolved source ID
URI
effective policy
resource kind
cache key
optional reviewed request headers
```

Suggested resource kinds:

```text
RasterTile
ProviderMetadata
AttributionMetadata
WmsCapabilities
WmtsCapabilities
StyleMetadata
```

Do not route local MBTiles through this service.

## Required cache/HTTP flow

```text
policy permits network?
    no -> typed denial

cache enabled + policy permits client cache?
    yes -> inspect cache

entry fresh?
    yes -> return cached

stale + validators?
    send If-None-Match / If-Modified-Since

304
    -> refresh metadata
    -> return cached body

200
    -> honor Cache-Control / Expires / no-store
    -> persist only if allowed
    -> return bytes

error
    -> typed HTTP/network/rate-limit result
```

Honor:

```text
Cache-Control
Expires
ETag
Last-Modified
```

Use policy fallback retention only when server metadata is absent and the reviewed provider policy defines one.

## User-Agent

Generate runtime identity from application/assembly version, e.g.:

```text
MissionPlanner/<actual-version> (+project/contact URL)
```

Do not hard-code a stale version such as `MissionPlanner/2.0`.

Do not impersonate a browser.

## Credential injection

Do not inject secrets in view models.

Use typed auth strategy metadata such as:

```text
None
QueryApiKey
AuthorizationBearer
HeaderApiKey
```

Values come from secure storage.

Redact all secret-bearing query parameters, authorization headers and signed URLs from logs and diagnostics.

## Planner cache settings

Wire:

```text
MapHttpCacheEnabled
MapHttpCacheLimitMiB
```

into the actual runtime cache.

Remove hard-coded budgets that ignore settings.

## Cache implementation hardening

Refactor `MapHttpDiskCache` so it does not recursively enumerate and sort all cache files on every tile write.

Use one of:

```text
maintained byte counter
threshold-triggered eviction
coalesced background eviction
small persistent index
```

Also ensure:

- exact per-source namespace;
- no prefix-collision source clearing;
- atomic writes;
- concurrency-safe same-key requests;
- corrupt cache entry handled locally;
- cache budget remains bounded.

## OSM runtime rule

OSM Standard must use:

```text
interactive viewing only
honest User-Agent
visible attribution
HTTP caching according to headers
no bulk prefetch
no offline-pack conversion
```

If a convenience BruTile source cannot honor this central path, replace it with an explicit source/fetcher adapter.

## Esri

Do not implement tile harvesting/offline creation.

Use the central runtime request path where technically practical and keep attribution metadata independently cacheable.

## Tests

Add tests for:

- fresh hit;
- stale + ETag -> 304;
- stale + Last-Modified -> 304;
- 200 replacement;
- no-store;
- cache disabled setting;
- policy-denied cache;
- cache budget/eviction;
- exact source clear;
- concurrent same-key requests;
- cancellation/timeouts;
- 401/403/429;
- credential redaction;
- runtime User-Agent;
- OSM offline/prefetch denial;
- provider-specific auth injection.

## Documentation

Update:

```text
docs/MAPS.md
docs/PLANNER_SETTINGS.md
docs/FEATURES.md
```

Clearly distinguish transient online cache from durable offline packs.

## Acceptance criteria

- Supported online map requests use central identity/policy/cache behavior.
- Planner cache settings affect the actual runtime cache.
- Hosted/custom factories cannot bypass cache/policy enforcement.

---

# Task 04 — Wire attribution into the live map

## Objective

Turn the existing attribution infrastructure into a visible, continuously correct map overlay.

## Dependency

Tasks 01–03.

## Current code to inspect

```text
IMapAttributionContributor
IMapAttributionService
MapAttributionService
MapAttributionOverlayState
EsriAttributionResolver
MapBasemapController.BasemapChanged
MissionMapView.xaml
Plan and FlightData map composition
```

## Coordinator

Add a UI-independent coordinator, for example:

```csharp
public interface IMapAttributionCoordinator
{
    MapAttributionOverlayState Current { get; }
    event EventHandler<MapAttributionOverlayState>? Changed;
    ValueTask RefreshAsync(CancellationToken cancellationToken = default);
}
```

Responsibilities:

1. track current resolved basemap;
2. gather attribution from visible basemap and operational data sources;
3. resolve dynamic provider attribution where needed;
4. deduplicate by stable ID/link;
5. produce compact and expanded forms;
6. refresh on source switch, layer visibility changes and metadata expiry/manual refresh.

Do not let the coordinator depend directly on a MAUI view.

## Mandatory-attribution behavior

When policy requires visible attribution, source activation must not silently produce an unattributed map.

Choose explicit behavior:

- use reviewed fallback attribution and report degraded metadata state; or
- deny activation if no legally sufficient attribution can be resolved.

Prefer reviewed fallback for Esri metadata outages.

## UI

Add one shared attribution view around the map:

```text
bottom corner
small/readable
semi-transparent background
light/dark aware
accessible
click/tap for expanded attribution
safe external link launcher
```

Do not duplicate separate Plan and FlightData attribution implementations if they share the map control/presenter.

## Esri dynamic attribution

Wire `EsriAttributionResolver` into the coordinator using the Task-03 HTTP resource fetcher.

Requirements:

- bounded timeout;
- cached metadata;
- fallback attribution;
- no single stale hard-coded string as the only source.

## Export contract

Expose the current attribution snapshot for future screenshot/PDF/static map export.

Do not invent a new screenshot feature if none currently exists.

## Tests

Add tests for:

- OSM;
- all current Esri built-ins;
- source switching;
- multiple contributors;
- deduplication;
- operational contributor visibility;
- dynamic resolver success;
- dynamic resolver failure/fallback;
- long compact/expanded text;
- mandatory attribution never disappears;
- repeated source switching.

## Documentation

Update `docs/MAPS.md` and `docs/FEATURES.md`.

## Acceptance criteria

- Attribution is visible on the actual mission map.
- Provider switching updates attribution.
- Multiple visible sources can contribute attribution.

---

# Task 05 — Harden offline pack installation, feed policy and provenance

## Objective

Make raster-MBTiles pack handling robust for large files, signed feeds, active-pack lifecycle and corrupt manifests.

## Dependency

Tasks 01–04.

## Current code to inspect

```text
OfflineMapPackManifest
OfflineMapPackInstaller
OfflineMapPackValidator
FileOfflineMapPackRepository
MapPackFeedClient
MapPackFeedInstaller
pack removal/update UI
active source selection
```

## Stream directly to staging

Every install path must use:

```text
input stream
    -> bounded copy
    -> SHA-256 while copying
    -> progress
    -> staged file
    -> validation
    -> manifest write
    -> atomic directory promote
```

Do not buffer complete map packs in RAM.

Use `long` for sizes.

Reject:

```text
more bytes than declared
fewer bytes than declared
hash mismatch
cancellation
disk/IO failure
```

Clean staging deterministically after failure.

## One installation primitive

User import, approved-feed download and any future source must reuse the same bounded staging/install path so no caller bypasses validation.

## Manifest provenance

Extend installed manifest schema with at least:

```text
SourceId
ProductId
PolicyId
Policy revision/review date
InstallOrigin: UserImported / ApprovedFeed
Safe provenance/source URI or text
RetrievedAt
Attribution IDs
License/notice references
```

Do not include credentials or signed secret URLs.

Older installed manifests remain readable. Missing provenance must be represented as `LegacyUnknown` or equivalent rather than invented.

## Signed feed policy enforcement

A valid signature proves authenticity, not permission.

Before accepting a feed entry:

1. resolve SourceId;
2. verify ProductId matches source;
3. resolve reviewed policy;
4. verify durable offline pack installation is allowed;
5. verify archive/content formats supported;
6. verify required attribution/license data present;
7. reject vector/PMTiles packs under ADR-0006.

## Active pack ownership

Do not rely on an optional `activePackId` argument that callers can omit.

Introduce an owning manager/service that knows current `SelectedSourceId` and handles install/update/remove.

Required behavior:

- active pack cannot be accidentally removed;
- upgrade of active pack has explicit source-switch behavior;
- inactive pack removal works;
- forced removal, if supported at all, must explicitly switch to fallback first.

## Corrupt manifest isolation

One corrupt manifest must not break enumeration of all installed packs.

Return valid packs plus diagnostics, or quarantine/skip invalid entries and surface a user-visible warning.

## MBTiles performance measurement

Profile current per-tile SQLite connection behavior before changing it.

Measure:

```text
connection open/close cost
pan/zoom latency
concurrent tile loads
file-handle stability
```

Only optimize if measurements justify it.

## Tests

Add tests for:

- zero-byte;
- exact size;
- one byte too large;
- truncated;
- hash mismatch;
- cancellation mid-copy;
- IO failure cleanup;
- very large simulated stream without full RAM buffering;
- valid signed feed;
- invalid signature;
- valid signature + invalid source/product/policy;
- PMTiles/vector feed rejected under ADR-0006;
- active pack removal denied;
- inactive pack removal;
- active pack upgrade;
- corrupt manifest isolation;
- old manifest migration;
- provenance round trip.

## Documentation

Update `docs/MAPS.md` and `docs/FEATURES.md`.

## Acceptance criteria

- Pack installation memory is bounded independently of pack size.
- Signed feeds cannot bypass reviewed source policy.
- Installed packs retain useful provenance.
- One corrupt pack cannot break the complete repository.

---

# Task 06 — Harden basemap switching and MAUI lifecycle

## Objective

Make source changes asynchronous, last-write-wins, UI-thread safe and resource-safe.

This task number is unrelated to the previous vector-production Task 06. ADR-0006 remains unchanged.

## Dependency

Tasks 01–05.

## Current code to inspect

```text
MapBasemapController
MissionMapPresenter
source-change callbacks
MapView lifecycle
page/tab lifecycle
layer/resource disposal
```

Look specifically for:

```text
ConfigureAwait(false) followed by Mapsui layer mutation
.GetAwaiter().GetResult()
async void source-switch methods
missing generation/cancellation handling
```

## Last-write-wins model

Use:

```text
monotonic generation
per-switch CancellationTokenSource
last requested source ID
current committed source ID
```

Scenario:

```text
request A
request B
```

B must win even if A completes later.

Do not serialize obsolete slow work ahead of newer user choices.

## UI-thread commit

Source resolution and archive/network creation may happen off the UI thread.

All Mapsui mutations must dispatch to the MAUI UI thread:

```text
insert replacement basemap
remove previous basemap
publish BasemapChanged
update attribution state
```

Never assume continuation thread after `await`.

## Atomic replacement

Required order:

1. resolve/create replacement fully;
2. confirm generation still current;
3. dispatch commit;
4. insert replacement;
5. remove old basemap;
6. dispose old source/layer resources;
7. publish committed result.

If creation fails, keep old basemap untouched.

If result became stale, dispose the unused replacement and do not touch current map.

## Presenter lifecycle

Remove synchronous blocking on async initialization.

Prefer explicit lifecycle:

```text
construct
attach
ActivateAsync
Deactivate
Dispose
```

or equivalent using existing MissionPlanner lifecycle abstractions.

Requirements:

- subscriptions attached once;
- pending switch cancelled on deactivation/disposal;
- no UI-bound collection clearing from Dispose;
- no fire-and-forget `async void` except framework event handlers that catch/report exceptions.

## Result model

Expose source-switch outcomes:

```text
Committed
Rejected
Cancelled
Stale
ResolutionFailed
CreationFailed
PolicyDenied
CredentialMissing
```

Do not show an error for discarded stale work.

## Operational-layer preservation

Add tests proving switches preserve:

```text
viewport
mission route layer identity
waypoints
home
vehicle
track history
fence/rally
ADS-B
POI
selected mission item where architecture permits
```

Do not replace the whole Mapsui `Map`.

## Resource disposal

Repeated switching must not leak:

```text
MBTiles SQLite/file handles
owned HTTP tile-source resources
event subscriptions
timers
temporary metadata resources
```

Do not dispose shared singleton HTTP/cache services from a basemap layer.

## Tests

Add tests for:

- A before B;
- B before A;
- A fails/B succeeds;
- stale A completes after B;
- rapid 20-source switch sequence;
- cancellation;
- UI-dispatch requirement;
- failure keeps old source;
- stale replacement disposed;
- viewport/operational layers preserved;
- MBTiles file removable after switching away;
- presenter activation/deactivation/disposal;
- no blocking async initialization path.

## Documentation

Update `docs/MAPS.md` with lifecycle and last-write-wins semantics.

## Acceptance criteria

- Last user source selection always wins.
- Mapsui layer commits occur on UI thread.
- Failed/stale work cannot destroy current map.
- Repeated switching does not leak pack/file resources.

---

# Task 07 — End-to-end integration, policy audit, platform verification and documentation

## Objective

Close the final gap between infrastructure and an actually completed MissionPlanner map subsystem.

## Dependency

Tasks 01–06.

## End-to-end runtime verification

Verify these through the real application path.

### Built-in

```text
NoMap
OSM Standard
Esri World Topographic
Esri World Physical
Esri Shaded Relief
Esri Dark Gray
```

### Offline

```text
import raster MBTiles
select it
restart app
selection persists
disable network
map remains usable
switch away
remove inactive pack
```

### Hosted

At least one provider path when credentials are available:

```text
configure credential
select source
render tiles
render attribution
exercise cache
invalid credential -> useful error
```

External provider secrets must not be required in CI.

### Custom/self-hosted

Verify at least custom XYZ.

Verify WMS/WMTS only if the current implementation claims production support.

### Switching

Exercise:

```text
OSM -> Esri -> MBTiles -> custom -> NoMap -> OSM
```

and verify operational overlays survive.

## DI runtime graph test

Add a deterministic test using the actual application configurator, for example:

```text
MapInfrastructureTests.DependencyInjectionResolvesRuntimeMapGraph
```

Resolve at least:

```text
catalog
policy evaluator
source resolver
credential abstraction
offline pack manager/repository
custom-source service/store
hosted-source service
HTTP resource fetcher/cache
attribution coordinator
composite Mapsui factory
```

Use fakes only where platform APIs such as secure storage/dispatcher make deterministic tests necessary.

## Settings tests

Cover:

```text
legacy migration
SelectedSourceId persistence
missing/deleted source fallback
cache enabled/disabled
cache-size persistence
credentials never serialized
offline pack selected across restart
```

## Policy-model cleanup

Audit `MapSourceCapabilities`, `MapUsagePolicy`, `MapOperation` and `MapPolicyEvaluator`.

Make operations one-to-one instead of collapsing distinct rights.

Required distinctions:

```text
InteractiveUse
ClientDiskCache
OfflineAreaDownload
BulkPrefetch
Proxy
Redistribution
StaticExport
Printing if retained
```

If `SupportsPrinting`/`AllowPrinting` exist, either add matching operation/evaluation/tests or remove dead fields.

Do not map `OfflineAreaDownload` and `BulkPrefetch` to one shared boolean.

## Remove brittle provider-ID parsing

Audit for code such as:

```text
StartsWith("stadia-")
StartsWith("maptiler-")
StartsWith("thunderforest-")
```

Replace provider inference with catalog relationships and typed authentication strategy.

Stable origin namespaces such as `pack:` and `custom:` may remain if intentionally documented.

## Presentation model cleanup

Map settings presentation models should expose typed state:

```text
RequiresNetwork
RequiresCredential
CredentialConfigured
IsSelectable
Availability
CachePolicy
OfflinePackAvailability
```

Display labels such as `Online` must be derived in presentation and never used for runtime decisions.

## Manual platform matrix

Update and actually run `MAPS_PLATFORM_VERIFICATION.md` on:

```text
Windows
Android
Mac Catalyst
```

Where each platform is available, verify:

```text
startup
OSM
all Esri built-ins
NoMap
offline MBTiles
custom XYZ
hosted provider when credentials available
provider switching
mission editing
waypoint selection/drag
vehicle marker
follow vehicle
pan/zoom
mouse/touch
dark/light theme
network loss/recovery
restart persistence
attribution overlay
cache clear
pack removal
```

Record only:

```text
Pass
Fail
NotRun + reason
```

Never mark unexecuted tests passed.

## Documentation reconciliation

Review all map docs, especially:

```text
docs/MAPS.md
docs/FEATURES.md
docs/README.md
docs/PLANNER_SETTINGS.md
docs/ARCHITECTURE_DECISION_RECORDS.md
ADR-0006
docs/MAPS_PLATFORM_VERIFICATION.md
docs/tasks/maps*
```

Use three distinct status labels/concepts:

```text
Infrastructure implemented
Runtime integrated
Manually verified
```

Correct claims that currently overstate:

```text
attribution display
HTTP/cache runtime integration
hosted provider integration
custom provider integration
offline pack live selection
cross-platform verification
```

## ADR-0006

Do not reverse or weaken ADR-0006.

Documentation must say in substance:

> PMTiles/vector support is deferred, not rejected. The catalog, policy,
> attribution and pack architecture remains format-neutral so vector support
> can be added later without redesigning the raster path. Current production
> offline support is raster MBTiles.

## Build/test expectations

Run repository-standard:

```text
dotnet restore
dotnet build
deterministic tests
NuGet vulnerability audit if part of project workflow
```

External commercial-provider tests remain manual/optional and must not make CI require secrets.

## Final acceptance criteria

- `SelectedSourceId` drives the real map.
- Built-in, offline and supported custom/hosted raster sources share one source-selection path.
- Policy, credential, cache and attribution rules are enforced at runtime.
- Operational layers survive source switching.
- Cache and packs remain distinct.
- Manual verification docs reflect only what was actually run.
- Documentation does not overstate vector/PMTiles support.
- ADR-0006 remains the governing decision.

---

# Recommended Codex execution discipline

For each task:

1. Read the relevant current implementation first; do not assume this task text exactly matches code after previous Codex commits.
2. List the files/classes that will change before editing.
3. Implement the smallest cohesive slice that satisfies the task.
4. Add/adjust deterministic tests in the same commit.
5. Update documentation in the same commit.
6. Build/test before committing.
7. Do not continue to the next task while acceptance criteria are failing.
8. Do not modify ADR-0006 except to add clarifying references that preserve its existing decision.

