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
* Two-mode waypoint list/editor (`MissionItemListView`, right column of the Plan page): **Simple** mode is the compact list (number, command, lat/lon/alt, up/down/delete); **Complete** mode mirrors the v1.38 grid — editable P1–P4/Lat/Lon/Alt cells applied back to the mission, Frame column, derived per-leg Dist/AZ/Grad % columns, and a header (visible in Complete mode only) with mission summary (item count, total distance) and editor settings (WP Radius, Loiter Radius, Default Alt, Alt Warn); Clear Mission toolbar button
* Mission file Load / Load and Append / Save in three formats: `.waypoints` and `.txt` (QGC WPL 110, v1.38-compatible) and `.mission` (JSON document with version/name/home/items) — format detected from file content on load, chosen via dialog on save; implemented as `IMissionFileCodec` in Core with round-trip tests
* Map pans/zooms to fit the whole mission after loading a file or reading from the vehicle

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
* File Load/Save → Load WP File, Load and Append, Save WP File (.waypoints/.txt QGC WPL, .mission JSON)
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

## Full waypoint editor

v1.38 hosts the full mission editor detail in a fold/unfold panel below the map. That
layout is not replicated: the right column hosts the two-mode `MissionItemListView`
(resizable via grid splitter) — Simple mode for the compact list, Complete mode for the
v1.38-style grid with header. Implemented in Complete mode: editable P1–P4/Lat/Lon/Alt
(applied to the mission on commit), **Command and Frame selects** per row (v1.38 mavcmd
naming; changing the selection converts the item in place — command list limited to the
domain-supported set: WAYPOINT, LOITER_UNLIM/TURNS/TIME, RETURN_TO_LAUNCH, LAND, TAKEOFF,
DO_CHANGE_SPEED; frames Absolute/Relative/Terrain), Dist/AZ/Grad % derived columns, header
with mission summary and WP Radius / Loiter Radius / Default Alt / Alt Warn inputs.

### Missing from the complete editor

* Commands beyond the domain set in the Command select (v1.38 offers the full firmware
  MAV_CMD list from mavcmd.xml — grows as `MissionCommand`/`FromProtocol` grow)
* Derived UTM Zone/Easting/Northing and MGRS columns; Angle column
* Row operations: Add Below, TagData
* WP Radius / Loiter Radius / Alt Warn are editor settings only — not yet fed into new
  items (acceptance radius) or validation warnings
* Verify Height (SRTM) and Spline-by-default (need terrain service / spline support)
* Mission type selector (Mission/Fence/Rally) driving the editor payload
* Cursor coordinate readout (Lat/Lng/UTM/MGRS + terrain elevation)

Also missing on the map itself: **spline waypoints** (needs the spline command in the
domain plus curved path rendering), left-click-to-add waypoint, draggable waypoint/home
markers, WP radius circles.

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



# SETUP

The Setup screen replaces v1.38's Initial Setup (`src-v.1.38/GCSViews/InitialSetup.cs` +
`ConfigurationView/Config*.cs` panels). New UI: `InitSetupView` (placeholder page).

### Status

* Nothing implemented yet — placeholder page only

### Missing (v1.38 feature inventory)

* **Install Firmware**: manifest-driven firmware download/flash (stable/beta/custom), board detection, bootloader handling; "Wizard" guided first-time setup
* **Mandatory Hardware**: Frame Class/Type selection, Accelerometer Calibration, Compass calibration (incl. onboard cal), Radio Calibration, Servo Output mapping, ESC Calibration, Flight Modes assignment, FailSafe configuration
* **Optional Hardware**: Battery Monitor (analog/smart), CompassMot, Range Finder/Sonar, Airspeed sensor, Optical Flow / PX4Flow, OSD, Camera Gimbal (Mount), Motor Test, Bluetooth, SiK Radio configuration, Antenna Tracker, Parachute, ADSB, DroneCAN/UAVCAN, Serial port mapping, GPS ordering, ESP8266, CubeID, Joystick

Note: calibration flows (accel/compass/radio/ESC) require interactive MAVLink command
sequences (MAV_CMD_PREFLIGHT_CALIBRATION etc.) that the domain does not implement yet.



# CONFIG

The Config screen replaces v1.38's Config/Tuning (`SoftwareConfig.cs`). New UI:
`Views/ConfigTuning` — tab views exist for GeoFence, Basic Tuning, Extended Tuning,
Onboard OSD, MAVFtp, Full Parameters List, Planner and CubeLan8PortSwitch, but only
**Full Parameters List** is implemented.

## Full Parameters List

### Status

* Loads metadata and parameters and merges these (see PARAMETERS.md)
* Currently a lot of buttons for testing purposes. Needs trimming

### Missing

* Extend validation/range support in param editor
* Write (send changed values to the vehicle)
* Load/save parameter files (.param), compare/diff
* Search/filter, favorites, modified-only view (v1.38 raw params features)

### Issues

* Parameter loading is way too slow (profile the metadata/value merge)

## Other Config tabs (Missing)

v1.38 feature inventory per tab; the new tab views are placeholders:

* **Flight Modes**: assign flight modes to RC switch positions (not present as a tab yet)
* **GeoFence**: fence enable/type/action, altitudes/radius (tab view exists, empty)
* **Basic Tuning**: simplified sliders (roll/pitch sensitivity, throttle hover, climb rates)
* **Extended Tuning**: per-vehicle PID editors (Copter/Plane/Rover), filters, TX tuning knob
* **Standard / Advanced Params**: "friendly" curated parameter lists with combos/sliders
* **Full Parameter Tree**: tree-grouped variant of the raw parameter editor
* **Planner**: GCS application settings (units, speech, layout, video, map options)
* **MAVFtp**: browse the vehicle's filesystem over MAVLink FTP (tab view exists, empty)
* **Onboard OSD**: OSD layout editor (tab view exists, empty)
* v1.38 extras to consider later: FFT analysis, REPL/Terminal, DroneCAN tooling



# SIMULATION

Replaces v1.38's Simulation screen (SITL). New UI: `SimulationView` (placeholder page).

### Status

* Placeholder page only
* Asset: `src/Tests/MissionPlanner.Simulator` already hosts a simulator used by the smoke
  tests — a candidate backend for an in-app SITL experience

### Missing (v1.38 feature inventory)

* Select vehicle model (multirotor, heli, plane, quadplane, rover, sub)
* Download prebuilt SITL binary (stable/dev) from the ArduPilot build server
* Launch SITL with home location/speedup parameters and auto-connect over TCP
* Multi-vehicle / swarm launch options



# HELP

### Status

* Placeholder page only

### Missing

* Version/about info, update check (v1.38: check for updates, changelog display)



# EXIT

### Status

Completed







