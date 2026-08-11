# Mission Map Task 09 — Mission elevation profile graph

## Objective

Implement:

```text
Elevation Graph
```

using the existing terrain subsystem.


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


## Existing infrastructure

Reuse:

```text
ITerrainElevationService
SrtmTerrainElevationService
SrtmHgtReader
mission route geometry
Planner unit settings
```

Do not create another terrain data reader.

## Application model

Add:

```text
IMissionElevationProfileService
MissionElevationProfileRequest
MissionElevationProfile
MissionElevationSample
MissionElevationLeg
TerrainProfileStatus
```

Each sample should contain:

```text
cumulative ground distance
lat/lon
terrain elevation
planned vehicle altitude
planned altitude reference
clearance above terrain when calculable
mission sequence/leg
terrain availability
```

## Sampling

Sample along navigation legs by distance.

Requirements:

- configurable or sensible bounded sample interval;
- hard maximum sample count;
- cancellation;
- skip/non-geographic commands;
- preserve command-to-leg association;
- handle missing SRTM tiles as unavailable gaps, not zero metres.

## Altitude semantics

Be precise about:

```text
MSL/global altitude
relative-to-home altitude
terrain-relative altitude
```

Use existing MissionPlanner altitude/frame conversion services if available.

If home altitude is required for relative clearance and unavailable:

```text
show planned relative profile
mark absolute clearance unavailable
```

Do not silently mix reference systems.

## UI graph

Create a cross-platform graph view using an existing chart dependency if already approved.

If no chart package exists, implement a lightweight `GraphicsView` profile renderer rather than adding a large dependency solely for one graph.

Display:

```text
distance x-axis
terrain profile
planned mission profile
optional clearance band/warning
mission waypoint markers
hover/tap sample details
missing-terrain gaps
```

Use Planner unit preferences.

## Performance

Profile generation may run off UI thread.

Publish one final profile plus bounded progress.

Cache terrain reads through existing terrain service behavior; do not duplicate SRTM files.

## Tests

Add:

- flat known terrain fake;
- varying terrain fake;
- route sampling;
- cumulative distance;
- relative/global/terrain frame semantics;
- missing terrain;
- no geographic mission;
- cancellation;
- max samples;
- units/format projection.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Document terrain source, altitude-reference caveats and missing-data behavior.

## Acceptance criteria

- Elevation Graph displays terrain and planned mission profiles.
- Missing terrain is explicit.
- Existing `ITerrainElevationService` is reused.
