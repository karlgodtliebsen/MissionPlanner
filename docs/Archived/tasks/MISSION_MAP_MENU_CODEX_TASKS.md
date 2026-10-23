# MissionMapView MenuFlyout — Codex Handoff


---

<!-- 00-README.md -->

# MissionMapView MenuFlyout completion — Codex task set

## Scope

The current `MissionMapView.axaml` contains 43 MenuFlyout actions still bound to `NotImplementedCommand`.

This package replaces those placeholders in deliberate functional groups rather than adding 43 unrelated methods to `MissionMapViewModel`.

Current missing menu actions:

```text
Advanced Mission Items
  Insert Spline WP
  Jump to Start
  Jump to WP #
  DO_SET_ROI

Polygon
  Draw a Polygon
  Clear Polygon
  Save Polygon
  Load Polygon
  Polygon from SHP
  Polygon from Current Waypoints
  Offset Polygon
  Polygon Area

Geo-Fence
  Upload
  Download
  Set Return Location
  Load from File
  Save to File
  Clear

Rally Points
  Set Rally Point
  Download
  Upload
  Clear
  Save to File
  Load from File

Auto WP
  Create WP Circle
  Create Spline Circle
  Area
  Text
  Create Circle Survey
  Survey (Grid)

Map Tools
  Measure Distance
  Rotate Map
  Prefetch
  Prefetch WP Path

Files / Terrain
  KML Overlay
  Elevation Graph
  Load KML File
  Load SHP File

POI
  Add
  Delete
  Edit

Location
  Tracker Home
  Enter UTM Coordinate
```

## Required architecture

Do not make `MissionMapViewModel` a monolith.

Introduce a planning interaction structure approximately like:

```text
MissionMapView
   │
   ▼
MissionMapViewModel
   │ commands delegate to
   ├─ Mission item editing service
   ├─ Polygon workspace
   ├─ Fence workspace/service
   ├─ Rally service
   ├─ Auto-WP/survey generators
   ├─ Map tool service
   ├─ Geospatial import service
   ├─ POI service
   └─ Tracker/location service

UI-neutral planning state
   ├─ MissionMapSnapshot
   └─ MissionPlanningOverlaySnapshot
            │
            ▼
MissionMapPresenter
   └─ Mapsui rendering only
```

Recommended common concepts:

```text
MissionMapInteractionMode
    None
    DrawPolygon
    MeasureDistance
    AddPoi
    SetFenceReturn
    SetRallyPoint
    SetTrackerHome
    ...

MissionPlanningOverlaySnapshot
    Polygon
    Measurement
    RallyPoints
    Fence preview
    POIs
    Imported overlays
    Survey preview
    Tracker home
```

Use the smallest set of interaction modes actually required.

## Existing code that must be reused

### Missions

```text
MissionMapView.axaml
MissionMapViewModel
MissionMapPresenter
MissionMapSnapshot
Mission
IMissionProtocolMapper
MissionTransferService
MissionFileCodec
```

### GeoFence

A complete fence subsystem already exists:

```text
IFenceConfigurationService
FenceConfigurationService
FencePlan
FenceArea
FenceGeometryValidator
FenceProtocolMapper
GeoFenceTabViewModel
GeoFenceMapView
```

The MissionMap context menu must reuse this state/service rather than implementing a second fence protocol stack.

### Maps / Terrain

```text
ITerrainElevationService
SrtmTerrainElevationService
MapCoordinateFormatter
current provider policy/cache infrastructure
```

### MAVLink

Generated dialect already contains relevant commands including:

```text
MavCmd.NavSplineWaypoint
MavCmd.DoJump
MavCmd.DoSetRoiLocation
MavCmd.DoSetRoi
MavCmd.NavRallyPoint
```

## Execution order

```text
00 interaction/overlay foundation
01 advanced mission items
02 polygon workspace
03 KML/SHP geospatial import and overlays
04 GeoFence menu integration
05 Rally points
06 Auto-WP basic generators
07 Survey generators
08 map tools and policy-aware prefetch
09 terrain elevation profile
10 POI repository and overlays
11 tracker home and UTM entry
12 final integration, command availability, tests and documentation
```

Commit after every task.

## Final completion rule

Task 12 must assert that:

```text
MissionMapView.axaml contains zero NotImplementedCommand bindings
```

and remove the generic placeholder command if it has no remaining legitimate use.


---

<!-- 00-interaction-overlay-foundation.md -->

# Mission Map Task 00 — Planning interaction and overlay foundation

## Objective

Create the shared interaction, dialog/file boundary and map-overlay infrastructure needed by the remaining MenuFlyout features without changing existing working mission commands.


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


## Current source state

Inspect before editing:

```text
MissionMapView.axaml
MissionMapView.axaml.cs
MissionMapViewModel.cs
MissionMapPresenter.cs
MissionMapSnapshot
```

The current `MissionMapViewModel` is already large and contains direct Avalonia calls such as:

```text
FilePicker
Application.Current...Page
DisplayPromptAsync
DisplayActionSheetAsync
Toast
```

Do not expand that pattern for 43 new commands.

## 1. Interaction state

Introduce a small UI-neutral interaction model, for example:

```csharp
public enum MissionMapInteractionMode
{
    None,
    DrawPolygon,
    MeasureDistance,
    SetFenceReturnLocation,
    SetRallyPoint,
    AddPoi,
    SetTrackerHome
}
```

Only add modes actually needed by later tasks.

Add a controller/state service:

```text
IMissionMapInteractionService
MissionMapInteractionState
```

Responsibilities:

- enter one interaction mode;
- cancel current interaction;
- accept map click/move/end events;
- expose prompt/status text;
- reject mutually exclusive interactions;
- clear temporary interaction state on vehicle/page lifecycle change.

The service must not reference Mapsui.

## 2. Planning overlay snapshot

Add an immutable/UI-neutral overlay model separate from the normal mission snapshot:

```text
MissionPlanningOverlaySnapshot
    DrawnPolygon
    TemporaryMeasurement
    FencePreview
    RallyPoints
    PoiItems
    ImportedOverlays
    SurveyPreview
    TrackerHome
```

Use small typed records rather than one giant bag of nullable objects where practical.

`MissionMapPresenter` becomes responsible for mapping these snapshots to Mapsui layers/features.

Do not put Mapsui geometry into Core/ViewModels.

## 3. Presenter layers

Create stable presenter-owned layers for categories such as:

```text
planning polygon
measurement
fence
rally
POI
imported KML/SHP visual overlays
survey preview
tracker home
```

Update features in place or replace layer contents; do not recreate the whole map.

All temporary layers must survive basemap changes.

## 4. Dialog/file abstractions

Introduce reusable application abstractions for the new feature set, for example:

```text
IUserPromptService
IUserChoiceService
IFileOpenService
IFileSaveService
IUserNotificationService
```

Reuse existing abstractions where equivalent ones already exist.

Do not create duplicate dialog services.

As later tasks touch existing direct `DisplayPromptAsync`/`FilePicker` paths, migrate those touched paths to the abstraction. Do not perform a huge unrelated UI rewrite.

## 5. Command availability

Add a typed command-availability model:

```text
MissionMapCommandAvailability
    IsEnabled
    Reason
```

Availability should eventually account for:

```text
current map interaction
mission state
active vehicle
vehicle family/capabilities
online/replay state
polygon present
fence/rally state
selected POI
map provider policy
terrain availability
```

Do not put provider-policy logic directly in XAML converters.

## 6. Lifecycle

- cancel temporary interaction when the MissionMap view is truly deactivated;
- do not clear stable UI-bound data during `Dispose()`;
- subscriptions must be deterministic;
- presenter layers/resources must be disposed without disposing shared map/cache services.

## Tests

Add deterministic tests for:

- one interaction at a time;
- cancel;
- mode transition;
- map click routing;
- temporary overlay state;
- overlay state survives basemap change;
- lifecycle cancellation;
- presenter layer identity preserved;
- dialog/file abstractions injectable in view-model tests;
- command availability reason propagation.

## Documentation

Create or extend `docs/MISSIONS.md` with a section:

```text
Mission map interaction architecture
Planning overlays
Menu command ownership
```

Update `docs/FEATURES.md` only for the new infrastructure; do not mark menu features complete yet.

## Acceptance criteria

- Later tasks can add map interactions without adding Mapsui types to Core.
- `MissionMapViewModel` delegates new functionality to focused services.
- Temporary overlay rendering is independent of basemap switching.
- Existing mission editing still works.


---

<!-- 01-advanced-mission-items.md -->

# Mission Map Task 01 — Spline waypoint, DO_JUMP and ROI mission items

## Objective

Implement the four advanced mission-item MenuFlyout actions:

```text
Insert Spline WP
Jump to Start
Jump to WP #
DO_SET_ROI
```


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


## Existing limitations

Current typed mission items/protocol mapper support only the basic mission command family.

Extend the typed mission domain rather than inserting raw numeric mission rows.

## 1. Spline waypoint

Add a typed mission item for:

```text
MavCmd.NavSplineWaypoint
```

Model the fields supported by MissionPlanner's current mission editor:

```text
lat
lon
altitude
frame/altitude reference
hold/delay if supported by existing mission editor semantics
```

Requirements:

- insert at clicked map position using existing insertion sequencing;
- use default/current mission altitude consistently;
- preserve spline item through:
  - `Mission.WithSequence`;
  - protocol mapping;
  - mission upload/download;
  - mission file save/load;
  - map rendering/labels.
- gate the creation UI by supported vehicle family/capability where appropriate.
- do not silently convert spline to normal waypoint on round trip.

## 2. DO_JUMP

Add a typed `JumpMissionItem` using:

```text
MavCmd.DoJump
```

Fields:

```text
TargetSequence
RepeatCount
```

Implement:

```text
Jump to Start
Jump to WP #
```

Validation:

- target exists;
- target is not the DO_JUMP item itself where invalid;
- repeat count follows MAVLink/ArduPilot semantics;
- support `-1` infinite only with explicit warning/confirmation;
- enforce/document ArduPilot's practical mission limit for DO_JUMP commands;
- preserve user-facing numbering versus zero-based MAVLink sequence correctly.

`Jump to Start` should derive the first executable mission item rather than blindly hard-coding a UI row index.

Mission reorder/delete must keep Jump target semantics coherent.

Choose and document one policy:

```text
A. Jump target sequence automatically tracks item identity when mission rows move;
or
B. target remains explicit numeric sequence and revalidation warns after reordering.
```

Prefer stable mission-item identity + sequence recalculation if the current domain can support it without excessive change.

## 3. ROI

For **new** location ROI items prefer:

```text
MavCmd.DoSetRoiLocation
```

because the generic:

```text
MavCmd.DoSetRoi
```

is legacy/superseded.

Add typed ROI location mission item:

```text
lat
lon
altitude
frame
```

Requirements:

- context-menu action uses clicked map location;
- make label/menu wording clearer if practical, e.g. `Set ROI Here`, while preserving user intent;
- protocol mapper writes modern ROI Location;
- downloader/file parser should preserve and understand legacy `DoSetRoi` where possible rather than dropping it;
- do not silently rewrite unsupported legacy ROI variants into location ROI unless semantics are equivalent.

## 4. Raw/unsupported preservation

Review `MissionFileCodec` and download mapper behavior.

Do not keep the current pattern where newly supported commands are skipped simply because no typed mapper existed.

If a generic unknown mission-item preservation model already exists, reuse it. Otherwise limit this task to the commands above and document remaining unsupported-command behavior.

## UI

Replace these four `NotImplementedCommand` bindings with real commands.

Use prompt abstractions from task 00 for:

```text
Jump target
Repeat count
infinite-repeat warning
```

Map item labels/icons should distinguish:

```text
WP
Spline
Jump
ROI
```

## Tests

Add tests for:

- spline wire round trip;
- spline file round trip;
- insert-at-location;
- Jump Start;
- Jump specific target;
- repeat `0`, positive, `-1`;
- invalid target;
- jump-count limit;
- reorder/delete behavior;
- ROI modern encoder;
- legacy ROI decode compatibility;
- sequence recalculation;
- upload/download round trip.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Document the new typed mission items and legacy ROI compatibility.

## Acceptance criteria

- All four menu items are functional.
- Mission upload/download/file round-trip preserves them.
- Modern ROI Location is used for newly created location ROI commands.
- No duplicate MAVLink numeric constants are introduced.


---

<!-- 02-polygon-workspace.md -->

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


---

<!-- 03-geospatial-import-kml-shp.md -->

# Mission Map Task 03 — KML/KMZ and SHP import/overlay

## Objective

Implement:

```text
KML Overlay
Load KML File
Load SHP File
Polygon from SHP
```


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


## Architecture

Add focused import services:

```text
IGeospatialImportService
KmlImportService
ShapefileImportService
GeospatialImportResult
GeospatialFeature
GeospatialImportPreview
```

Keep file parsing and coordinate transformation outside the view model.

The result must distinguish:

```text
Point
LineString
Polygon
MultiGeometry
unsupported geometry
```

and retain source feature names/attributes where useful.

## Dependency review

Before adding libraries, inspect current project dependencies.

For each new library, record:

```text
package
version
license
maintenance status
security audit result
why it is needed
```

Prefer maintained modern packages.

Do not copy legacy SharpKML/ProjNet/DotSpatial source wholesale into the new solution.

## KML Overlay

Support `.kml` and, if the chosen parser safely supports it, `.kmz`.

Overlay behavior:

- render imported points/lines/polygons as a non-mission visual layer;
- preserve feature names/descriptions where practical;
- allow remove/replace overlay;
- overlay survives basemap switching;
- file/network links inside KML must not trigger arbitrary downloads/execution;
- bound file/decompression size for KMZ;
- reject path traversal in archives.

Do not automatically turn overlay geometry into mission items.

## Load KML File

This menu command is a **mission import** operation.

Provide an import preview showing:

```text
points found
line strings found
polygon features
number of mission items to be created
unsupported items
```

Recommended mapping:

- Point placemarks -> candidate mission waypoints or POIs; user chooses when ambiguous.
- LineString coordinates -> ordered mission waypoints.
- Polygon -> do not silently turn every boundary vertex into a mission unless user explicitly chooses that conversion.

Import into current mission only after confirmation.

Do not destroy the existing mission without explicit Replace/Append choice.

## Shapefile import

Support `.shp` with required companion files resolved safely.

At minimum:

```text
.shp
.dbf when attributes needed
.prj when present
```

Coordinate reference system handling:

- if `.prj` exists, parse and transform to WGS84;
- if CRS is absent, do not blindly assume a projected coordinate system;
- allow explicit user confirmation of WGS84 only when coordinates plausibly match it;
- reject unknown CRS with actionable message.

Legacy compatibility worth preserving where reasonable:

- `ELEVATION`/`alt` field can supply waypoint altitude;
- Z coordinate may supply altitude;
- optional `wp` attribute can influence ordering.

Do not make these legacy field names the only supported schema.

## Load SHP File

Create mission waypoint candidates from point/line geometry with preview.

For polygon geometry, offer explicit choice:

```text
Use as planning polygon
Create waypoints from boundary
Cancel
```

Do not silently conflate these.

## Polygon from SHP

Select suitable polygon geometry from the file and load it into `IPlanningPolygonService`.

For multi-polygon files provide feature selection/preview.

## Security

Bound:

```text
file size
feature count
vertex count
attribute lengths
archive expansion for KMZ
```

Do not resolve remote external resources from KML by default.

## Tests

Use small checked-in fixtures:

- WGS84 KML points/line/polygon;
- KMZ if supported;
- malformed XML;
- remote-resource KML ignored;
- SHP WGS84;
- SHP projected + `.prj`;
- missing `.prj`;
- altitude attributes;
- multi-feature/multi-polygon;
- malformed companion files;
- path traversal/decompression limits;
- Replace/Append mission import;
- planning-polygon import.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Document supported KML/SHP geometry and CRS limitations.

## Acceptance criteria

- Overlay and mission import are separate deliberate workflows.
- SHP coordinate systems are transformed correctly or rejected explicitly.
- `Polygon from SHP` populates the shared planning polygon workspace.


---

<!-- 04-geofence-menu-integration.md -->

# Mission Map Task 04 — Integrate existing GeoFence subsystem into MissionMap

## Objective

Implement all Geo-Fence MenuFlyout commands by reusing the already-complete fence subsystem:

```text
Geo-Fence Upload
Geo-Fence Download
Geo-Fence Set Return Location
Geo-Fence Load from File
Geo-Fence Save to File
Geo-Fence Clear
```


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


## Critical constraint

Do **not** create another MAVLink fence implementation.

Reuse:

```text
IFenceConfigurationService
FenceConfigurationService
FencePlan
FenceArea
FenceAreaKind
FenceGeometryValidator
FenceProtocolMapper
existing operation gate
MAV_MISSION_TYPE_FENCE support
```

MissionMap and Config/Tuning GeoFence must operate on the same conceptual fence plan/state.

## Shared fence workspace

Inspect current ownership in `GeoFenceTabViewModel`.

If the config tab currently owns local edit state privately, extract the minimum shared application workspace needed so:

```text
MissionMap menu
Config/Tuning GeoFence
```

can see/modify the same local fence plan without duplicating protocol state.

Do not make a static global singleton detached from active vehicle.

## Download

Call the existing fence service download flow.

Requirements:

- current active vehicle;
- supported typed geometry;
- replay disabled for vehicle operation;
- cancellation;
- show progress/result;
- update shared local plan;
- render fence overlay in MissionMap.

If local edits exist, require explicit conflict choice or use existing revision/backup semantics.

## Upload

Upload/apply the shared local fence plan through `IFenceConfigurationService.ApplyAsync`.

Requirements:

- validate geometry first;
- operation gate;
- connection/replay checks;
- confirmation summarizing inclusion/exclusion areas and return point;
- preserve/read back result through existing service behavior.

## Set Return Location

Use task-00 map interaction:

```text
SetFenceReturnLocation
```

Next accepted map click updates only the local `FencePlan` return point.

Do not immediately upload.

## Load/save file

Prefer one versioned MissionPlanner fence JSON format capable of representing:

```text
return point
polygon inclusion
polygon exclusion
circle inclusion
circle exclusion
```

If existing fence file serialization exists, reuse it.

Do not use a lossy polygon-only format.

Load should update local plan only, with validation and confirmation if local changes exist.

Save is local-only and must work offline.

## Clear

Distinguish:

```text
Clear local fence plan
Clear fence on vehicle
```

The existing MenuFlyout wording `Geo-Fence Clear` is ambiguous.

Improve UX so the command cannot unexpectedly erase vehicle state.

Recommended flow:

1. prompt:
   - Clear local plan only
   - Clear vehicle fence
   - Cancel
2. vehicle clear uses `IFenceConfigurationService.ClearAsync` and strong confirmation.

## Overlay

Render current/local fence plan through a stable fence planning overlay.

Use different visual treatment for:

```text
inclusion
exclusion
return point
dirty/local state when useful
```

## Tests

Add tests for:

- shared state between MissionMap and GeoFence Config view-models;
- download;
- upload;
- set return point interaction;
- load/save round trip;
- inclusion/exclusion circles/polygons;
- invalid geometry;
- dirty-local conflict;
- replay denial;
- disconnect/cancel;
- clear-local versus clear-vehicle distinction;
- overlay update.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Cross-reference existing fence documentation.

## Acceptance criteria

- All six MissionMap GeoFence commands use the existing fence protocol implementation.
- Config/Tuning and MissionMap do not maintain competing fence states.
- Vehicle-destructive clear/upload operations are explicit and confirmed.


---

<!-- 05-rally-points.md -->

# Mission Map Task 05 — Rally point domain, protocol and MenuFlyout

## Objective

Implement:

```text
Set Rally Point
Rally Points Download
Rally Points Upload
Clear Rally Points
Save Rally to File
Load Rally from File
```


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


## Existing protocol support

Reuse the current generic mission-transfer infrastructure where appropriate:

```text
MissionPlanType.RallyPoints
MavMissionType.Rally
MissionTransferService
generated MavCmd.NavRallyPoint
```

There is currently no dedicated Rally domain/application service, so create one.

## Domain

Add:

```text
RallyPoint
RallyPointId
RallyPlan
RallyPlanRevision
RallyPlanSnapshot
IRallyConfigurationService
IRallyWorkspace
```

A rally point should represent:

```text
latitude
longitude
altitude
altitude/frame semantics
optional stable local identity
```

Keep MAVLink sequence separate from stable UI identity where practical.

## Protocol mapper

Add a dedicated mapper between:

```text
RallyPoint
MAV_CMD_NAV_RALLY_POINT
MAV_MISSION_TYPE_RALLY
```

Support the Global/Relative/Terrain altitude frames accepted by current ArduPilot rally-point handling, using existing MissionPlanner altitude/frame concepts where possible.

Do not reuse the normal flight mission `Mission` class if that would blur plan-type semantics.

## Workspace/revisions

Follow the good Fence pattern:

```text
vehicle revision
local revision
dirty state
last download
```

Avoid direct mutation of a bound list from disposal.

## Set Rally Point

Use task-00 map interaction:

```text
SetRallyPoint
```

At map click:

1. prompt/default altitude and altitude reference;
2. validate;
3. add to local rally plan;
4. render immediately;
5. do not upload automatically.

## Download/upload

Download using mission protocol with `MAV_MISSION_TYPE_RALLY`.

Upload only after:

```text
active vehicle
connected
not replay
valid plan
confirmation
operation gate
```

Handle firmware unsupported responses explicitly.

## Clear

Distinguish:

```text
clear local rally plan
clear vehicle rally points
```

Vehicle clear is destructive and requires confirmation.

## File format

Add versioned JSON:

```text
schemaVersion
vehicle/firmware provenance optional
points
altitude reference
createdAt
```

Atomic save/load.

Loading changes only local rally plan until user uploads.

## Overlay

Render:

```text
rally marker index/name
altitude
selected marker
```

Use stable separate layer.

## Tests

Add:

- rally command mapper;
- each supported frame;
- download/upload round trip;
- unsupported firmware;
- set interaction;
- local/vehicle revision semantics;
- clear local vs vehicle;
- file round trip;
- malformed file;
- reconnect/cancellation/replay;
- overlay ordering.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Document that Rally is a separate MAVLink mission plan type from the flight mission.

## Acceptance criteria

- All Rally menu commands work end-to-end.
- Rally points never get mixed into the normal flight mission upload.
- Local edits are explicit before upload.


---

<!-- 06-auto-wp-generators.md -->

# Mission Map Task 06 — Auto-WP circles, spline circles, area and text

## Objective

Implement:

```text
Create WP Circle
Create Spline Circle
Auto WP Area
Auto WP Text
```

`Create Circle Survey` and `Survey (Grid)` are task 07.


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


## Shared architecture

Add a platform-neutral generation service:

```text
IAutoWaypointGenerator
AutoWaypointGenerationRequest
AutoWaypointGenerationResult
GeneratedMissionPreview
```

Generators return candidate typed mission items.

They must **not** mutate the mission until preview validation and explicit Apply/Append/Replace choice.

## Create WP Circle

Input:

```text
center = context map location
radius metres
number of points
direction clockwise/counter-clockwise
start angle
altitude
altitude reference
```

Validation:

```text
radius > 0
bounded point count
finite values
valid latitude
```

Generate normal waypoint items around the circle using geodesic destination calculations.

Do not approximate longitude scaling with fixed degree arithmetic.

Preview circle + generated points before applying.

## Create Spline Circle

Use typed `NavSplineWaypoint` support from task 01.

Preserve useful legacy behavior while making it explicit:

```text
center
radius
point count or angular spacing
direction
start angle
minimum altitude
maximum altitude
altitude step / climb profile
```

If a helical climb is selected, produce deterministic altitude progression.

If preserving the legacy center-ROI behavior:

- use modern `DoSetRoiLocation`;
- make `Point camera/ROI at center` an explicit option;
- do not insert legacy generic `DoSetRoi`.

## Auto WP Area

Do not duplicate area math.

Delegate to task-02 polygon area calculation.

If no polygon exists:

```text
disabled with reason
or prompt user to draw/create one
```

## Auto WP Text

Do not port the legacy Windows/System.Drawing + external `1CamBam_Stick_3` font dependency.

Implement a deterministic, cross-platform stroke font.

Recommended:

```text
small embedded Hershey-like/single-line vector font data
```

Inputs:

```text
text
origin
height/scale in metres
rotation
letter spacing
line spacing if multiline
altitude
```

Generate waypoint paths representing strokes.

Handle pen-up transitions deliberately and document the resulting travel path.

Avoid absurd missions:

```text
maximum characters
maximum generated points
minimum segment spacing
mission item limit check
```

Preview before applying.

## Mission merge

All generators support explicit:

```text
Append
Replace
Cancel
```

unless a generator clearly only makes sense as append.

Do not silently clear the mission.

## Tests

Add:

- circle known bearings/distances;
- clockwise/counter-clockwise;
- start angle;
- high latitude fixtures;
- spline altitude progression;
- optional ROI center;
- area delegation;
- stroke-font deterministic glyph fixtures;
- rotation/scale;
- max-point limit;
- preview/apply semantics;
- mission sequence correctness.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Document the cross-platform text generator and mission-size limits.

## Acceptance criteria

- Four menu actions are implemented without platform-specific font/rendering dependencies.
- Generated missions are previewed before mutation.
- Geodesic calculations are used.


---

<!-- 07-survey-generators.md -->

# Mission Map Task 07 — Circle Survey and Grid Survey

## Objective

Implement:

```text
Create Circle Survey
Survey (Grid)
```

using platform-neutral geometry/planning services.


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


## Legacy references

Read-only reference material includes:

```text
src-v.1.38/Utilities/CircleSurveyMission.cs
src-v.1.38/ExtLibs/MissionPlanner.Gridv2/GridUIv2.cs
src-v.1.38/ExtLibs/Utilities/Grid.cs
```

Understand legacy planning behavior but do not port WinForms/plugin UI code.

## Survey domain

Add:

```text
ISurveyMissionGenerator
SurveyArea
GridSurveyRequest
CircleSurveyRequest
SurveyMissionResult
SurveyLeg
SurveyStatistics
```

Reuse task-02 planning polygon for area/grid survey.

## Grid Survey

At minimum support:

```text
planning polygon
flight-line angle
line spacing
overshoot/lead-in
altitude
altitude reference
start corner/optimization option
cross-grid optional
```

If camera metadata/calculations already exist elsewhere, reuse them.

If not, keep v1 scope based on explicit line spacing/altitude rather than implementing an entire camera-calibration subsystem in this task.

Requirements:

- clip flight lines to polygon;
- order legs deterministically;
- avoid zero-length legs;
- support concave polygon where chosen geometry engine permits;
- preview path;
- calculate:
  - total distance;
  - estimated number of mission points;
  - area;
  - line count.

## Circle Survey

Implement a concentric/orbit style survey centered at context location or polygon-derived center.

Define explicit inputs after inspecting the legacy algorithm:

```text
center
radius / radial spacing
altitude
point spacing
direction
number of rings or inner/outer radius
```

If legacy circle survey is camera-footprint driven, preserve useful calculations only when their required inputs can be represented cleanly.

Do not invent opaque magic defaults without documenting them.

## Mission commands

Generated navigation items must use typed mission items/protocol mapper.

If camera trigger mission commands are added:

- use generated MAVLink enums;
- add typed mission items and round-trip tests;
- only include them when the user explicitly enables triggering.

## Preview and apply

Use `MissionPlanningOverlaySnapshot.SurveyPreview`.

Show:

```text
area
flight path
direction arrows when practical
start/end
estimated distance
point count
```

Apply only after confirmation.

Support:

```text
Append
Replace
Cancel
```

## Limits

Validate against:

```text
maximum mission items
minimum line spacing
finite geometry
too-small polygon
self-intersecting polygon
extreme latitude/projection limitations
```

## Tests

Add fixture polygons:

```text
rectangle
rotated rectangle
concave L shape
small polygon
invalid polygon
```

Verify:

- clipping;
- line spacing;
- angle;
- ordering;
- cross-grid;
- overshoot;
- deterministic output;
- point count;
- circle survey geometry;
- preview/apply;
- cancellation.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Document the supported v1 survey parameters and known limitations versus legacy Mission Planner.

## Acceptance criteria

- Both survey menu entries generate usable previewed missions.
- Geometry code is platform-neutral and unit tested.
- No WinForms/plugin dependencies are introduced.


---

<!-- 08-map-tools-prefetch.md -->

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


---

<!-- 09-elevation-profile.md -->

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

If no chart package exists, implement a lightweight Avalonia custom-control profile renderer rather than adding a large dependency solely for one graph.

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


---

<!-- 10-poi.md -->

# Mission Map Task 10 — Persistent Points of Interest

## Objective

Implement:

```text
POI Add
POI Delete
POI Edit
```

with a typed persistent POI repository and map overlay.


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


## Domain/application model

Add:

```text
PointOfInterest
PointOfInterestId
IPoiRepository
IPoiService
PoiSnapshot
```

Suggested fields:

```text
Id
Name
Latitude
Longitude
Altitude optional
Description optional
Category optional
CreatedAt
UpdatedAt
```

POIs are local MissionPlanner planning data, not MAVLink mission items.

Do not use a static/global mutable collection.

## Persistence

Use versioned JSON in app data.

Requirements:

```text
atomic save
schema version
bounded file size/count
valid finite coordinates
stable IDs
migration path
corrupt file isolation/backup
```

If the application already has a suitable local-data repository abstraction, reuse it.

## Add

Use context-click location or task-00 `AddPoi` interaction.

Prompt:

```text
name
optional altitude/description/category
```

Provide sensible default name.

## Edit

The menu command needs a POI target.

Support one of:

```text
nearest POI to clicked location within screen tolerance
selected POI
explicit list chooser
```

Prefer explicit selected/nearest + confirmation rather than editing an arbitrary first POI.

## Delete

Delete selected/nearest POI with confirmation.

No vehicle interaction.

## Overlay

Render persistent POIs on their own stable Mapsui layer.

Requirements:

- labels;
- selected state;
- click hit-testing;
- basemap changes do not remove them;
- reload across application restart.

## Import interaction

Task 03 KML point imports may offer:

```text
Import as POIs
```

through `IPoiService`.

Do not duplicate KML parsing here.

## Tests

Add:

- repository round trip;
- atomic save failure;
- corrupt file;
- add/edit/delete;
- coordinate validation;
- duplicate names;
- selection/hit target behavior;
- KML import handoff;
- restart persistence;
- overlay updates;
- disposal does not clear bound collection.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Document that POIs are local and not uploaded to the aircraft.

## Acceptance criteria

- Add/Edit/Delete work and persist.
- POIs survive basemap switching and application restart.
- POIs are not mixed into flight mission upload.


---

<!-- 11-tracker-home-utm.md -->

# Mission Map Task 11 — Tracker Home and UTM coordinate entry

## Objective

Implement:

```text
Tracker Home
Enter UTM Coordinate
```


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


## 1. Tracker Home

The legacy feature stored a local tracker/antenna home location.

The current new application has no confirmed antenna-tracker transport subsystem.

Therefore implement a truthful local planning state first:

```text
TrackerHome
ITrackerHomeService
TrackerHomeSnapshot
```

Fields:

```text
latitude
longitude
altitude optional
updatedAt
source
```

The MenuFlyout action:

1. uses context-click map position;
2. optionally prompts altitude;
3. stores tracker-home state;
4. renders a distinctive tracker-home marker.

Do **not** claim that this commands physical tracker hardware.

If Codex discovers an actual current tracker service during implementation:

- integrate through that existing typed service;
- keep local tracker-home state separate from hardware ACK/state;
- update documentation accordingly.

Persistence may be added to Planner settings or a small local state file if useful; document whether it is persisted.

## 2. UTM inverse conversion

Current `MapCoordinateFormatter` supports WGS84 -> UTM/MGRS display.

Add proper inverse:

```text
UTM -> WGS84
```

through a dedicated typed geodesy service, for example:

```text
IGeodeticCoordinateConverter
UtmCoordinate
GeographicCoordinate
```

Do not put parsing/calculation in the view model.

## UTM parser

Accept explicit:

```text
zone number 1..60
hemisphere N/S
easting
northing
```

Optionally support common compact input:

```text
32N 500000 6170000
```

Do not conflate UTM zone letters/bands with hemisphere without validation.

Requirements:

```text
finite values
valid zone
valid easting/northing ranges
round-trip accuracy tests
```

Use a reviewed geodesy/projection implementation.

If adding ProjNet or another package, perform dependency/license/security review.

## Enter UTM Coordinate workflow

1. prompt or dialog for zone/hemisphere/easting/northing;
2. convert to WGS84;
3. show conversion preview:
   - lat/lon;
   - map marker;
4. user chooses:
   - Add waypoint here
   - Center map here
   - Cancel

If legacy behavior always added a waypoint, preserve that as the default action but make the result explicit.

Altitude uses current/default mission altitude when adding waypoint.

## Tests

Add reference fixtures from known UTM coordinates covering:

```text
Denmark
northern hemisphere
southern hemisphere
zone boundaries
invalid zone
invalid easting/northing
round trip WGS84 -> UTM -> WGS84
```

Also test:

- tracker home set/update;
- marker state;
- no hardware command when no tracker subsystem exists;
- UTM waypoint insertion sequence.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Document tracker-home semantics honestly and UTM input conventions.

## Acceptance criteria

- Tracker Home stores/renders a real local state rather than a placeholder.
- UTM input converts accurately and can create a waypoint.
- No fake antenna-tracker command path is introduced.


---

<!-- 12-final-menu-integration-docs.md -->

# Mission Map Task 12 — Final MenuFlyout integration, safety, tests and documentation

## Objective

Complete the MissionMap MenuFlyout implementation after tasks 00–11, remove all placeholder command bindings and verify cross-feature behavior.


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


## 1. Zero placeholder bindings

Search:

```text
MissionMapView.axaml
all MissionMap-related XAML
MissionMapViewModel
```

Acceptance condition:

```text
0 MenuFlyout items bound to NotImplementedCommand
```

If `NotImplementedCommand` has no legitimate remaining consumer, remove it and `NotImplemented(string feature)`.

Do not replace it with a differently named generic TODO command.

## 2. Command grouping and naming

Review MenuFlyout organization after real implementation.

Use clear groups:

```text
Mission Items
Polygon
Geo-Fence
Rally Points
Auto WP / Survey
Map Tools
Import / Overlay
POI
Location
```

Clarify ambiguous labels where safe:

```text
DO_SET_ROI -> Set ROI Here
Geo-Fence Clear -> explicit local/vehicle choice
Load KML File -> Import Mission from KML
KML Overlay -> Add KML Overlay
```

Preserve discoverability for users familiar with legacy Mission Planner.

## 3. Dynamic availability

Every command must have meaningful enablement.

Examples:

```text
Spline:
    supported vehicle family

Jump:
    mission has suitable target(s)

Polygon Area/Offset/Save:
    polygon exists

GeoFence Upload:
    connected, not replay, valid local fence

Rally Upload:
    connected, not replay, valid local rally plan

Survey:
    valid polygon exists

Prefetch:
    effective map policy allows bulk prefetch

Elevation Graph:
    mission has geographic navigation legs

POI Edit/Delete:
    POI selected/near context click
```

Disabled actions should expose a reason where practical.

## 4. Context location semantics

Audit all actions that use:

```text
last right-click/context point
current vehicle
current map center
```

Use one explicit `MissionMapContext` snapshot.

Avoid accidentally using a stale click from another view/tab.

On touch platforms where right-click does not exist, ensure commands have a usable alternate interaction.

## 5. Dirty state / undo

Review mission/polygon/fence/rally modifications.

At minimum provide consistent dirty-state feedback.

Where existing mission undo support exists, integrate generated/imported modifications.

Do not build a giant new undo framework if none exists; document current limitations.

## 6. Vehicle/replay safety audit

Local-only commands may work during replay:

```text
polygon
measure
rotate
local POI
KML/SHP overlay
UTM conversion
elevation profile from loaded mission
```

Vehicle-changing commands must be disabled appropriately:

```text
GeoFence upload/download/clear
Rally upload/download/clear
```

Mission local editing may remain available during replay but mission upload must stay prohibited through existing upload safeguards.

## 7. Cancellation

Long operations must expose cancellation/progress:

```text
SHP/KML import
survey generation when large
terrain profile
prefetch
fence/rally transfer
```

Navigating away cancels transient work without clearing bound result collections in `Dispose()`.

## 8. Central limits

Add centralized limits/configuration for:

```text
max imported file size
max geospatial feature count
max polygon vertices
max generated mission items
max survey lines/points
max text-generator points
max terrain samples
max prefetch tiles
```

Limits should fail with actionable messages rather than freezing the UI.

## 9. DI composition

Extend production configurator and deterministic DI tests to resolve all new services:

```text
interaction service
polygon service
geospatial import
fence workspace integration
rally service/workspace
auto-WP generator
survey generator
map tools/prefetch
elevation profile
POI repository/service
tracker home
coordinate converter
dialog/file abstractions
```

Choose lifetimes deliberately.

Vehicle-specific mutable state must not leak between vehicles.

## 10. Cross-feature tests

Create end-to-end deterministic scenarios:

### Mission planning

```text
draw polygon
generate grid survey
append mission
add spline
add jump
save mission
reload mission
all commands preserved
```

### Fence

```text
set return
upload fake protocol
download
state remains shared with Config/Tuning
```

### Rally

```text
set local point
save
reload
upload/download fake protocol
```

### Geospatial

```text
SHP polygon -> planning polygon -> survey
KML line -> mission preview -> append
KML points -> POIs
```

### Map/local tools

```text
measure
rotate
UTM -> waypoint
terrain profile
POI persistence
tracker home
```

### Map policy

```text
OSM prefetch denied
provider allowing bulk prefetch -> cache warming succeeds
no offline-pack writes
```

## 11. Manual cross-platform verification

Update a mission-map verification matrix for:

```text
Windows
Android
macOS
```

Verify MenuFlyout/context interaction equivalents for mouse and touch.

Do not mark unrun checks passed.

## 12. Documentation

Update at minimum:

```text
docs/MISSIONS.md
docs/FEATURES.md
docs/README.md
docs/MAPS.md
```

`docs/MISSIONS.md` should contain:

```text
mission item support matrix
context-menu feature guide
polygon workspace
GeoFence integration
Rally plan type
Auto-WP/surveys
KML/SHP support
map measurement/prefetch
elevation profile
POIs
Tracker Home
UTM entry
file formats
safety/replay rules
known limitations
```

Clearly distinguish:

```text
Implemented
Runtime integrated
Manually verified
```

## 13. Legacy parity audit

Compare current MenuFlyout against legacy FlightPlanner only to ensure no existing XAML item remains stubbed.

Do not add unrelated legacy menu features that are not present in the current new MissionMap menu.

Create a final table in `docs/FEATURES.md`:

```text
Menu item
Implementation owner/service
Status
Tests
Known limitations
```

## Acceptance criteria

- `NotImplementedCommand` is absent from every MissionMap MenuFlyout item.
- All 43 previously missing actions have real behavior.
- No feature is implemented solely in code-behind with direct MAVLink calls.
- Existing basic mission editing and new map-provider architecture still work.
- Deterministic tests pass.
- Documentation reflects actual implementation and manual verification status.
