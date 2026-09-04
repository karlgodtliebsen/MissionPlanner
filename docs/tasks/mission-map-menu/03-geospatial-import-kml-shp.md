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
