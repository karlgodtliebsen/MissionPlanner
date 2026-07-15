# Domain



## MavLink

### Status

* Near Completed

## Vehicle



### Status

* Complete Domain Model



## Messages

### Status

* Compete set of Messages



## Mission

### Status

* In progress. Needs completion











\# UI





FlightPlanner



\## HUD:

\### Status

Implemented a responsive HUD



\### Missing

More details in overlay





\## Tabset

Only Quick Implemented at the moment



\### Quick



\### Status



\### Missing









\## Connection



\### Status



SERIAL/USB implemented and tested



\### Missing

TCP/UDP needs testing

Other







\## Flight Map:



\### Status



A responsive Map

Right Click Menu





\### Missing



Stubbed (menu item present, shows "not implemented yet" toast): 

Insert Spline WP, 

Jump,

DO\_SET\_ROI,

Polygon, 

Geo-Fence, 

Rally Points,

Auto WP (survey etc.), 

Measure/Rotate/Prefetch/KML/Elevation, 

Load KML/SHP, 

POI, 

Tracker Home, 

Enter UTM Coord. 



"Switch Docking" (WinForms-specific) and "GDAL Opacity" (no GDAL in the new stack) were dropped. 



Load WP File / Load and Append / Save WP File are implemented (QGC WPL 110 via IMissionProtocolMapper.ToProtocol/FromProtocol). 

Jump/ROI commands are still missing from the MissionCommand enum; files containing them load with those lines skipped.









# PLAN (FlightPlan)

The Plan screen is the dedicated mission editor, replacing `src-v.1/GCSViews/FlightPlanner.cs`.
Feature inventory below comes from the old FlightPlanner code (designer + handlers) and the
official docs (https://ardupilot.org/planner/docs/mission-planner-flight-plan.html).

New UI: `FlightPlannerView` / `FlightPlannerViewModel`. The mission editor itself is the shared
`Views/Missions/MissionMapView` + `MissionMapViewModel` (singleton) — the FlightData map hosts the
same control and the same mission plan, so edits made on either screen show up on both.

## Old FlightPlanner feature inventory

### Map area

* Left-click adds a waypoint at the default altitude; markers are draggable (move WP, drag home)
* Right-click context menu (Delete/Insert WP, Spline WP, Loiter, Jump, RTL, Land, Takeoff, ROI, Clear, Polygon, Geo-Fence, Rally, Auto WP, Map Tool, File Load/Save, POI, Tracker Home, Modify Alt, UTM, Set Home)
* Cursor readout: Lat/Lng, UTM, MGRS, terrain elevation (SRTM)
* Distance readouts: distance to home, distance to previous WP, total mission distance
* WP radius / loiter radius circles, spline path rendering, grid overlay checkbox
* Map provider selection (comboBoxMapType: Google/Bing/OSM/custom, etc.), zoom level control + slider
* Inject Custom Map (custom tile/WMS source), offline map prefetch, KML overlay, View KML link

### Action panel / toolbar

* Read / Write mission to vehicle
* Write Fast (mission-type MISSION only; skips per-item verify; optional MAVFTP transport checkbox)
* Load File / Save File (QGC WPL waypoint files)
* Mission type selector: Mission / Fence / Rally
* Altitude mode selector: Relative / Absolute / Terrain
* Verify Height (adjust altitudes against SRTM terrain), Alt Warn threshold
* WP Radius, Loiter Radius, Default Alt inputs
* Spline-by-default checkbox
* Home Location panel (lat/lng/alt, set from vehicle or map click)

### Waypoint grid

* One row per mission item: Command (full MAV\_CMD combo), Frame, P1–P4, Lat, Lon, Alt
* Derived columns: UTM Zone/Easting/Northing, MGRS, Grad %, Angle, Dist, AZ
* Row operations: move Up/Down, Delete, Add Below, TagData
* Grid and map stay in sync while editing

### Advanced tools

* Polygon: draw, clear, save/load, from SHP, from current WPs, offset, area
* Geo-Fence editor: polygon + circular inclusion/exclusion zones, upload/download, file save/load, return location
* Rally points: set/upload/download/clear, file save/load
* Auto WP: WP circle, spline circle, circle survey, survey grid, area, text
* Elevation profile graph, measure distance, POI management, antenna tracker home, UTM coordinate entry

## Status (implemented in the new app)

* Core Missions domain: `Mission` aggregate; items for Waypoint, Takeoff, Land, RTL, ChangeSpeed, Loiter (unlimited/time/turns); validation; plan types Mission/Geofence/RallyPoints
* Protocol mapping in both directions (`IMissionProtocolMapper.ToProtocol` / `FromProtocol`) with round-trip tests
* Plan page (`FlightPlannerView`): shared mission map + toolbar with Load File / Save File, Read / Write / Write Fast (wired to `IMissionTransferService` via `IVehicleRegistry`, with upload progress and validation before Write; Write Fast skips validation), map type select box, Default Alt input, status readouts
* Mission item list panel on the Plan page: number, command, lat/lon/alt per row, move up/down, delete, synced with the map
* Shared mission editor (`MissionMapView`/`MissionMapViewModel` singleton, hosted by both FlightData and Plan): right-click menu, insert/delete WP, Loiter x3, RTL, Land, Takeoff, Clear Mission, Reverse WPs, Set Home Here, Modify Alt, default altitude
* WP file Load / Load and Append / Save (QGC WPL 110) with FilePicker/FileSaver
* Map: Mapsui with switchable tile sources (OpenStreetMap + 4 keyless Esri layers), mission pins + route polyline, follow-vehicle toggle, zoom in/out, center on vehicle, center on user GPS location (also on startup when no vehicle fix)

## Missing for the Plan UI

* Read/Write against a live vehicle is untested end-to-end (wired, needs a connected vehicle/simulator pass)
* In-place grid cell editing (command combo, frame, P1–P4, lat/lon/alt per row) — the list is currently read-only apart from reorder/delete
* Bing/Google/custom WMS map providers with API-key handling (only keyless OSM/Esri sources today)
* Home location panel (lat/lng/alt entry, set from vehicle)
* Altitude mode selector (Relative/Absolute/Terrain) — terrain requires an SRTM/terrain elevation service
* WP Radius / Loiter Radius / Default Alt / Alt Warn settings surfaced in the UI
* Mission type selector (Mission/Fence/Rally) — domain enum exists, editors for Fence/Rally do not
* Draggable waypoint markers and left-click-to-add on the map
* Distance readouts (home/prev/total) and cursor coordinate readout (Lat/Lng/UTM/MGRS + terrain)
* Spline waypoints, Jump and DO\_SET\_ROI commands (require `MissionCommand` enum additions)
* Polygon tools, Geo-Fence editor, Rally point editor, Auto WP / survey generation
* Elevation profile, measure distance, prefetch/offline maps, KML/SHP import, POI, tracker home, UTM entry, Verify Height



\# SETUP







\# CONFIG



\## Full Parameters List



\### Status

* Loads Meta data and Parameters and merges these
* Currently a lot of Buttons for testing purposes. Needs trimming



\### Missing





* Extend validation/range support in param editor
* Write





\### Issues:

Parameter loading is way to slow







\# SIMULATION





\# HELP







\# EXIT

\### Status

Completed







