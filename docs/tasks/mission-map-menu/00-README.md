# MissionMapView MenuFlyout completion — Codex task set

## Scope

The current `MissionMapView.xaml` contains 43 MenuFlyout actions still bound to `NotImplementedCommand`.

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
MissionMapView.xaml
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
MissionMapView.xaml contains zero NotImplementedCommand bindings
```

and remove the generic placeholder command if it has no remaining legitimate use.
