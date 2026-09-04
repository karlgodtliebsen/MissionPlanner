# Mission Map Task 02 — Polygon planning workspace

## Objective

Implement:

```text
Draw a Polygon
Clear Polygon
Save Polygon
Load Polygon
Polygon from Current Waypoints
Offset Polygon
Polygon Area
```

`Polygon from SHP` is implemented in task 03 because it belongs to geospatial import.


## Repository constraints

- Work only in the new MissionPlanner implementation under `src/`, `docs/`, `scripts/`, and test-data folders.
- Treat `src-v.1.38/` as read-only reference material. Never modify, format, move, or include legacy files in a commit.
- `src-v.1.38/GCSViews/FlightPlanner.cs` and legacy utilities/plugins may be read to understand historic Mission Planner behavior, but new code must be cross-platform .NET 10 / Avalonia architecture, not a WinForms port.
- Do not add the missing features as another 40+ methods inside the already-large `MissionMapViewModel`.
- Keep MAVLink wire/protocol concerns in `MissionPlanner.MavLink`, domain/application workflows in `MissionPlanner.Core`, map/source infrastructure in `MissionPlanner.Maps`, and Mapsui/Avalonia presentation in `MissionPlanner.AvaloniaUI.App`.
- Views and code-behind may handle native pointer/file/dialog boundaries but must not send MAVLink commands directly.
- Reuse generated `MavCmd`, `MavMissionType`, frames, messages and the current mission transport/ACK infrastructure. Do not create duplicate numeric MAVLink constants.
- Outbound vehicle operations must be active-vehicle scoped, connection-aware, cancellation-aware, operation-gated where appropriate, and disabled during telemetry-log replay.
- Do not mutate UI-bound observable collections from `Dispose()`.
- Commands that modify the mission or vehicle must support explicit validation, user-visible results and undo/cancel/preview where appropriate.
- Preserve all existing working MissionMapView behavior.
- Add deterministic tests with every task.
- Update `docs/FEATURES.md` and mission-planning documentation with every implemented slice.


## Domain/application design

Add a dedicated polygon workspace:

```text
IPlanningPolygonService
PlanningPolygon
PlanningPolygonSnapshot
PlanningPolygonOperationResult
```

Use a UI-neutral coordinate type already present in MissionPlanner, or introduce one shared geospatial point record if needed.

The workspace is **planning state**, not the flight mission itself.

Do not confuse it with `FencePlan`.

## Draw polygon

Use task-00 `DrawPolygon` interaction mode.

Behavior:

```text
enter draw mode
map clicks append vertices
visual preview updates
user explicitly Finish / Cancel
minimum 3 unique vertices
validate geometry
```

Allow editing/removing last vertex if practical.

## Geometry validation

Add/test:

```text
minimum points
duplicate points
self-intersection
degenerate area
longitude wrapping/dateline behavior where feasible
```

Prefer one shared geometry utility used later by surveys/fence conversions.

If adding a geometry dependency such as NetTopologySuite, perform dependency/license/security review and isolate it in platform-neutral geometry infrastructure. Do not add a large GIS dependency without justification.

## From current waypoints

Create polygon vertices from mission items that contain meaningful geographic positions.

Requirements:

- exclude commands without location;
- preserve mission order;
- require at least three valid positions;
- do **not** automatically clear the mission;
- if legacy behavior offered clearing, make it a separate explicit confirmation/choice.

## Offset polygon

Prompt for signed offset distance in user units.

Perform geometric offset in a locally appropriate projected/metre coordinate system.

Do not offset latitude/longitude by naïve degree arithmetic.

Requirements:

- inward/outward;
- handle offset collapse;
- preserve polygon winding consistently;
- preview result before replacing current polygon;
- report invalid/self-intersecting result.

## Area

Calculate geodesic/projected area robustly.

Display at least:

```text
m²
hectares
km² when useful
acres
ft² when imperial display is useful
```

Use Planner unit preferences for the primary display.

## Save/load

Define a small versioned MissionPlanner polygon JSON format containing:

```text
schemaVersion
name
createdAt
coordinates
optional metadata
```

Use atomic save.

Load validation must reject malformed/non-finite coordinates and unreasonable file size.

If an existing general map-geometry file format already exists, reuse it instead of inventing another.

## Overlay

Render:

```text
vertices
closed outline
semi-transparent fill
active edit vertex if applicable
```

through `MissionPlanningOverlaySnapshot`.

## Tests

Add tests for:

- draw complete/cancel;
- minimum vertices;
- self-intersection;
- from mission waypoints;
- mission with mixed location/non-location commands;
- positive/negative offset;
- collapsed offset;
- area known fixtures;
- JSON round trip;
- malformed file;
- overlay lifecycle.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Document polygon workspace semantics and that it is independent from GeoFence until explicitly applied/converted.

## Acceptance criteria

- All listed polygon actions except SHP import are implemented.
- Geometry operations use metre/geospatial math, not degree offsets.
- Polygon state is reusable by survey and fence workflows.
