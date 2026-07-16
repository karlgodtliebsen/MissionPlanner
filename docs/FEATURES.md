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






# FLIGHT DATA

The FlightData screen (`Views/FlightData`), replacing `src-v.1.38/GCSViews/FlightData.cs`:
HUD + tabset on the left, shared mission map on the right.

## HUD

### Status

* Implemented a responsive HUD (SkiaSharp-rendered)

### Missing

* More details in overlay (v1.38 shows: battery %, GPS fix text, distance readouts, EKF/vibe indicators, warning banners)

## Flight Map

### Status

* Hosts the shared mission editor (`MissionMapView`, see PLAN section) — responsive Mapsui map, right-click menu, mission pins + route, follow vehicle, center on user GPS location

### Missing

* Guided-mode actions from v1.38's flight map right-click menu: **Fly To Here**, Fly To Here Alt, Fly To Coords, Point Camera Here, Trigger Camera NOW
* Flight track trail (breadcrumb of where the vehicle has flown)
* v1.38 overlays: WP radius circles, tracking arrows, ADSB aircraft, POI markers

## Tabset

All tab views exist (`Views/FlightData/Tabs`), but only **Quick** has an implementation.
Feature description per tab comes from v1.38 (`FlightData.Designer.cs` tab pages).

### Quick

#### Status

* Implemented: large telemetry readouts (yaw/pitch/roll, heading, air/ground speed, altitude, climb, distance to WP, distance to MAV)

#### Missing

* v1.38 is user-configurable: double-click a cell to pick any telemetry field from the full status list; configurable row/column count

#### Issues

* **Dist to WP** and **DistToMAV** — fix implemented (2026-07-16), needs verification against a vehicle:
  DistToMAV was hardcoded 0; now computed as distance home→vehicle (v1.38 "DistToHome" semantics) from the
  new HOME_POSITION message (decoded, stored in `VehiclePositionState`, requested at connect via
  MAV_CMD_GET_HOME_POSITION). Dist to WP decoding now tolerates MAVLink v2 zero-truncated
  NAV_CONTROLLER_OUTPUT payloads instead of dropping them (stale values).

### Actions (Missing)

* Set flight mode (mode combo + Set Mode), quick mode buttons (Auto / Loiter / RTL)
* Arm/Disarm, Do Action (MAV_CMD list: takeoff, land, camera trigger, …)
* Change speed and altitude in flight, Restart Mission, Set current WP
* Joystick enable, Clear Track, Resume Mission

### Messages (Missing)

* Scrolling STATUSTEXT log from the vehicle (severity-colored)

### PreFlight (Missing)

* Pre-flight checklist/warning items (user-configurable conditions with pass/fail display)

### Gauges (Missing)

* Analog instruments: airspeed, ground speed, altitude, climb rate, heading compass

### Transponder (Missing)

* ADS-B transponder (uAvionix/ping) status and control: squawk code, ident, mode

### Status (Missing)

* Grid of every live telemetry/status variable (v1.38 `CurrentState` dump), filterable

### Servo/Relay (Missing)

* Manual servo output set high/low/toggle per channel; relay on/off buttons

### Aux Function (Missing)

* Trigger ArduPilot auxiliary functions (camera trigger, gripper, landing gear, …)

### Scripts (Missing)

* Run user scripts against the vehicle (v1.38 used IronPython; new equivalent TBD — consider Lua/C# scripting)

### Payload Control (Missing)

* Gimbal pan/tilt/zoom control, camera trigger, payload actions

### Telemetry Logs (Missing)

* Tlog playback: load, play/pause, speed control, scrubbing; export KML/graph data

### DataFlash Logs (Missing)

* Download dataflash logs over MAVLink, browse/graph, automatic log analysis

## Connection

### Status

* SERIAL/USB implemented and tested (see VEHICLE_CONNECTION.md)

### Missing

* TCP/UDP needs testing
* Auto-reconnect on link loss
* Other transports from the long-term list (Bluetooth, DroneBridge, ELRS)









# PLAN (FlightPlan)

The Plan screen is the dedicated mission editor, replacing `src-v.1.38/GCSViews/FlightPlanner.cs`.
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
* WP file Load / Load and Append / Save (QGC WPL 110) with FilePicker/FileSaver

### Issues

* Read/Write against a live vehicle is untested end-to-end (wired, needs a connected vehicle/simulator pass)
* Map redraw after mission changes — fix implemented (2026-07-16), needs visual verification: the route
  `Polyline` was mutated in place, which raises no collection-change notification, so the MapView never
  rebuilt its drawables layer; the polyline instance is now replaced in `Drawables` on each redraw.

## Flyout menu (right-click context menu)

Complete inventory of the `MissionMapView` context menu, mirroring v1.38.

### Implemented

* Delete WP (nearest to cursor, else last)
* Insert WP → Here / At Current Position (vehicle)
* Loiter → Forever / Time / Circles
* RTL, Land, Takeoff (altitude prompt)
* Clear Mission
* Map Tool → Zoom In, Zoom Out, Zoom To Vehicle, Center on My Location, Follow Vehicle (on/off), Reverse WPs
* File Load/Save → Load WP File, Load and Append, Save WP File (QGC WPL 110)
* Modify Alt (all items + default)
* Set Home Here

### Missing (stubbed — menu item present, shows "not implemented yet" toast)

* Insert Spline WP (needs spline command in `MissionCommand` + domain item)
* Jump → Start / WP # (needs DO_JUMP=177 in `MissionCommand`)
* DO_SET_ROI (needs ROI command in `MissionCommand`)
* Polygon → Draw / Clear / Save / Load / From SHP / From Current Waypoints / Offset / Area
* Geo-Fence → Upload / Download / Set Return Location / Load from File / Save to File / Clear
* Rally Points → Set / Download / Upload / Clear / Save to File / Load from File
* Auto WP → Create WP Circle / Create Spline Circle / Area / Text / Create Circle Survey / Survey (Grid)
* Map Tool → Measure Distance, Rotate Map, Prefetch, Prefetch WP Path, KML Overlay, Elevation Graph
* File Load/Save → Load KML File, Load SHP File
* POI → Add / Delete / Edit
* Tracker Home, Enter UTM Coord

### Dropped intentionally

* Switch Docking (WinForms-specific), GDAL Opacity (no GDAL in the new stack)

## Below-map collapsible waypoint panel (Missing)

v1.38 has a fold/unfold panel under the map (collapse button `˅`) hosting the full mission
editor detail. None of it is implemented yet:

* Waypoint grid: one row per item with Command (full MAV_CMD combo), Frame, P1–P4, Lat, Lon, Alt — in-place editable, synced live with the map (the Plan page's read-only item list is the seed for this)
* Derived grid columns: UTM Zone/Easting/Northing, MGRS, Grad %, Angle, Dist, AZ
* Row operations: Up/Down, Delete, Add Below, TagData
* Settings row: WP Radius, Loiter Radius, Default Alt, Alt Warn, Verify Height (SRTM), Spline-by-default checkbox, altitude mode selector (Relative/Absolute/Terrain)
* Home Location panel (lat/lng/alt entry, set from vehicle or map click)
* Mission type selector (Mission/Fence/Rally) driving the editor payload
* Distance/total readouts and cursor coordinate readout (Lat/Lng/UTM/MGRS + terrain elevation)
* Fold/unfold behavior itself (MAUI equivalent: an Expander or grid-splitter row)

Also missing on the map itself: left-click-to-add waypoint, draggable waypoint/home
markers, WP radius circles and spline path rendering.

## Map providers

v1.38 uses GMap.NET with dozens of providers; the new app uses Mapsui + BruTile.

### Implemented (keyless, ToS-friendly, via BruTile predefined sources)

* OpenStreetMap (default)
* Esri World Topo
* Esri World Physical
* Esri Shaded Relief
* Esri Dark Gray

### Missing — and why

| Provider (v1.38) | Reason not yet implemented |
|---|---|
| Google Map / Satellite / Hybrid / Terrain (+ China variants) | v1.38 scrapes Google's tile servers, which violates Google's ToS; a compliant version needs a Google Maps Platform key and their SDK terms don't fit an open GCS well. Decision needed. |
| Bing Road / Aerial / Hybrid | Requires a Bing Maps API key; BruTile supports Bing — needs key configuration UI/storage. |
| Mapbox (incl. user styles, NoFly) | Requires an access token; easy via BruTile URL template once token config exists. |
| Statkart Topo (Norway), Eniro (Nordics), Japan GSI (9 layers) | Keyless public endpoints; implementable as BruTile `HttpTileSource` URL templates — just not prioritized yet. |
| Yandex, AMap, Here/Ovi, NearMap, Czech, Lithuania | Regional providers with key/ToS constraints; add on demand. |
| GIBS Arctic/Antarctic, Esri Arctic, ArcGIS Arctic Bathymetry, Earthbuilder | Niche polar/science layers; add on demand. |
| Custom WMS / WMTS + "Inject Custom Map" | Needs a small "add custom source" UI; BruTile has WMS-C/WMTS support to build on. |
| GDAL (local GeoTIFF/raster) | Dropped: native GDAL dependency doesn't fit the cross-platform MAUI stack. Revisit with a managed raster reader if offline imagery becomes a requirement. |
| NoMap (blank background) | Trivial; add when offline workflows land. |

### Missing — map infrastructure

* Offline tile cache / prefetch (BruTile supports persistent caches; not wired)
* API-key management (settings storage + per-provider configuration)
* Attribution display for tile sources



\# SETUP







\# CONFIG



\## Full Parameters List



\### Status

* Loads Meta data and Parameters and merges these
* Currently a lot of Buttons for testing purposes. Needs trimming



\### Missing





* Extend validation/range support in param editor
* Write





### Issues:

Parameter loading is way to slow







# SIMULATION





# HELP







# EXIT

### Status

Completed







