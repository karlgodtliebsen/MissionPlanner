# Mission Map Task 08 — Measure, rotate and policy-aware prefetch

## Objective

Implement:

```text
Measure Distance
Rotate Map
Prefetch
Prefetch WP Path
```


## Repository constraints

- Work only in the new MissionPlanner implementation under `src/`, `docs/`, `scripts/`, and test-data folders.
- Treat `src-v.1.38/` as read-only reference material. Never modify, format, move, or include legacy files in a commit.
- `src-v.1.38/GCSViews/FlightPlanner.cs` and legacy utilities/plugins may be read to understand historic Mission Planner behavior, but new code must be cross-platform .NET 10 / MAUI architecture, not a WinForms port.
- Do not add the missing features as another 40+ methods inside the already-large `MissionMapViewModel`.
- Keep MAVLink wire/protocol concerns in `MissionPlanner.MavLink`, domain/application workflows in `MissionPlanner.Core`, map/source infrastructure in `MissionPlanner.Maps`, and Mapsui/MAUI presentation in `MissionPlanner.App`.
- Views and code-behind may handle native pointer/file/dialog boundaries but must not send MAVLink commands directly.
- Reuse generated `MavCmd`, `MavMissionType`, frames, messages and the current mission transport/ACK infrastructure. Do not create duplicate numeric MAVLink constants.
- Outbound vehicle operations must be active-vehicle scoped, connection-aware, cancellation-aware, operation-gated where appropriate, and disabled during telemetry-log replay.
- Do not mutate UI-bound observable collections from `Dispose()`.
- Commands that modify the mission or vehicle must support explicit validation, user-visible results and undo/cancel/preview where appropriate.
- Preserve all existing working MissionMapView behavior.
- Add deterministic tests with every task.
- Update `docs/FEATURES.md` and mission-planning documentation with every implemented slice.


## 1. Measure Distance

Use an explicit measurement interaction rather than hidden static state.

Recommended flow:

```text
activate Measure
click first point
move/click second point
display live/final:
    geodesic distance
    initial bearing/azimuth
optional:
    terrain/elevation difference when available
Finish/Cancel
```

Render temporary line and endpoint markers.

Use Planner unit preferences for display.

## 2. Rotate Map

Prompt or present small angle control:

```text
0..359 degrees
Reset North
```

Apply to Mapsui viewport/bearing through presenter/UI adapter.

Do not store rotation in domain mission state.

Decide whether rotation is persisted in Planner UI settings; if not, document it as session-only.

## 3. Prefetch architecture

This feature must comply with the **new map provider-policy architecture**.

Do not reproduce legacy bulk tile downloading blindly.

Add/reuse:

```text
IMapTilePrefetchService
MapPrefetchRequest
MapPrefetchEstimate
MapPrefetchResult
```

Before enabling:

```text
current source resolved
source is online
effective policy AllowBulkPrefetch == true
cache enabled
raster source supports tile enumeration
```

Explicitly deny:

```text
OSM Standard community tile service
offline MBTiles
any provider whose reviewed policy denies bulk prefetch
vector/PMTiles deferred source
```

Prefetch populates **online HTTP cache only**.

It must never create an offline pack or move cached tiles into `Maps/Packs`.

## Visible-area Prefetch

Before download:

1. derive current viewport bounds and zoom range;
2. enumerate tile count;
3. show estimate:
   - tile count;
   - zoom levels;
   - approximate known/unknown size;
   - provider/cache policy;
4. require explicit Start;
5. support cancellation/progress;
6. enforce hard tile-count limit.

Respect the central HTTP fetch/cache pipeline.

## Prefetch WP Path

Build a corridor around the current mission route.

Inputs:

```text
corridor width
minimum/maximum zoom
```

Enumerate only tiles intersecting corridor.

Do not use one huge bounding box when a route corridor can avoid unnecessary downloads.

## Concurrency/rate behavior

- use bounded concurrency;
- respect HTTP 429 / Retry-After;
- do not retry aggressively;
- share central provider credentials and HTTP identity;
- cancel on app shutdown/source change when appropriate.

## Tests

Add:

- geodesic distance/bearing;
- measure interaction;
- rotation;
- provider allows prefetch;
- OSM denial;
- offline-source denial;
- cache-disabled denial;
- visible bounds tile enumeration;
- route corridor enumeration;
- hard tile limit;
- cancellation;
- 429 handling;
- no pack directory writes;
- source changes mid-prefetch.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/MAPS.md
docs/FEATURES.md
```

Explicitly document that prefetch is provider-policy-controlled cache warming, **not offline-pack creation**.

## Acceptance criteria

- Measure and rotate work cross-platform.
- Prefetch commands are unavailable/denied when provider policy does not explicitly permit bulk prefetch.
- OSM Standard cannot be bulk prefetched.
