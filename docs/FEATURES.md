# Domain



## MavLink

### Status

* Near Completed

## Vehicle



### Status

* Complete Domain Model

* Connection-scoped vehicle firmware identity from HEARTBEAT and AUTOPILOT_VERSION, including semantic version, ArduPilot family, protocol capabilities, and hardware IDs
* Derived vehicle display names such as `SysID 1:Copter`, synchronized into connection and top-bar presentation state



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

### Shared infrastructure

#### Status

* Implemented (2026-07-22): `IActiveVehicleContext` exposes one immutable active-vehicle snapshot,
  online state, change notification, and a cancellation token scoped to the current connection.
* Implemented: lifecycle-aware Flight Data tabs lazy-initialize on their first online activation,
  dispose vehicle subscriptions when hidden or reconnected, and stop in-flight work on disconnect.
* Implemented: reusable async-operation/command-result states cover busy, success, warning, error,
  timeout, and disconnected outcomes; framework-neutral notifications are adapted to MAUI in the UI layer.
* Implemented: the Flight Data status strip displays the derived vehicle name, connection state,
  heartbeat freshness, and map-position freshness. Quick telemetry starts only while Quick is visible.

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

### Actions

#### Status

* Implemented (2026-07-22): safety-aware Arm, Disarm, firmware-family-specific Set Mode,
  Takeoff, Land, RTL, Loiter/Hold, reboot autopilot, and Set Home Here actions.
* Every action targets the active `VehicleId`, passes a Core safety policy, prevents another
  concurrent command for that vehicle, and displays pending, terminal COMMAND_ACK, timeout,
  denial, and telemetry-confirmed state. In-progress ACKs retain their correlation until a
  terminal result arrives.
* Hazardous operations require explicit confirmation. Takeoff and home changes require fresh
  position/GPS state; flight actions require fresh landed/armed state. Raw `COMMAND_LONG`
  execution is isolated in a validated, clearly marked expert section.
* ArduCopter, ArduPlane, Rover, and ArduSub mode choices come from separate Core catalogs, so
  Copter custom-mode numbers are never reused for another family.

#### Remaining

* Change speed and altitude in flight, Restart Mission, Set current WP
* Joystick enable, Clear Track, Resume Mission

### Messages

#### Status

* Implemented (2026-07-22): bounded, configurable, per-vehicle `STATUSTEXT` history that
  preserves severity, complete timestamp, source system/component, MAVLink 2 message ID,
  assembly state, and explicit truncation state.
* MAVLink 1 text is handled as a single frame. MAVLink 2 chunks are assembled by source,
  ID, and sequence with duplicate suppression, interleaved-ID isolation, missing-chunk
  detection, and timeout flushing.
* The Messages tab supports exact severity filters, case-insensitive search, pause/resume
  auto-scroll, clear-current-view, selection, copy selected/all, and UTF-8 text/JSON export.
  Severity is distinguished with text and symbols as well as styling hooks.
* Local application/workflow notifications use a separate bounded stream and are explicitly
  labeled as non-MAVLink. Action-command denial, timeout, and rejected ACK feedback is routed
  into this stream. Histories remain isolated by vehicle and survive temporary disconnects.
* Capacity and chunk timeout can be overridden with `VehicleMessages:Capacity` and
  `VehicleMessages:ChunkTimeout`; defaults are 500 messages per vehicle and two seconds.

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

### Telemetry Logs

#### Status

* Implemented (2026-07-23): Mission Planner `.tlog` indexing and read-only playback with
  play/pause, indexed seek, 0.1–50× speed, a recorded-time replay clock, decoded/rejected
  frame statistics, and replay-only vehicle state.
* Replay frames use the generated MAVLink parser/decoder and cohesive telemetry handlers,
  but own a separate registry and event boundary. Replay vehicles cannot enter the live
  command, mission, parameter, or MAVFTP routes.
* A global `REPLAY · READ ONLY` label appears in persistent chrome and action surfaces.
  While a log is loaded, the MAVLink connection boundary prohibits every outbound frame;
  closing replay explicitly restores live/simulation sends.

#### Remaining

* Start/stop recording of live raw MAVLink frames with retention policy
* Export KML and graph-ready data

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
domain plus curved path rendering), draggable waypoint/home markers, and WP radius circles.

Implemented 2026-07-17: optional **click map to add WP** mode in the Plan toolbar. Newly
created waypoints use the configured WP acceptance radius, and new loiter items use the
configured loiter radius.

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
`ConfigurationView/Config*.cs` panels). The new UI is the vehicle-aware `InitSetupView`.

### Status

* Implemented (2026-07-22): responsive workflow-card shell for Firmware, Frame,
  Accelerometer, Compass, Radio, Flight Modes, Battery, ESC, Servo Output, Optional
  Hardware, Safety, and Summary.
* Workflow relevance is evaluated from connection state, firmware family, MAVLink
  capabilities, and known parameter names. Recommended dependencies guide users without
  forcing a rigid wizard.
* The shell exposes Available, Unsupported, Not Connected, Not Started, In Progress,
  Completed, Warning, and Failed states, plus shared confirmation, progress, error, and
  connection-cancellation behavior for later workflows.
* Completion evidence is local only and keyed to a stable vehicle identity. Firmware or
  parameter changes invalidate that evidence to Warning so vehicle state is always
  revalidated. Related workflows link to existing Config pages instead of duplicating
  configuration editors.
* Workflow hosts are created lazily and discarded across vehicle changes; page lifecycle
  detaches parameter/vehicle subscriptions and cancels active work.
* The Mandatory Hardware shell composes twelve dedicated, strongly typed workflow
  `ContentView` components. The shell retains selection and shared page concerns, while
  each component resolves, binds, activates, and deactivates its own transient section
  ViewModel. Workflow markup, presentation state, cancellation, and section-specific
  behavior remain isolated without duplicating domain services. Hidden sections detach
  their event subscriptions and perform no background loading.
* Firmware workflow implemented (2026-07-22): displays the complete protocol-reported
  vehicle/firmware/board identity, including semantic version and release type, Git hash,
  board/vendor/product identifiers, UID/UID2, MAVLink version, and named/raw capabilities.
* Firmware manifests support stable, beta, and development channels, technical board
  targets, exact vendor/product/board matching, release notes, HTTPS package locations,
  and SHA-256 checksums. Marketing names are never used to infer a flash target.
* Firmware downloads use a platform cache and fail closed on checksum mismatch. A guarded
  state machine requires verification and a confirmed parameter backup before disconnecting
  normal MAVLink, invoking `IFirmwareFlashingService`, and detecting post-flash reconnect.
  The shipped adapter explicitly blocks flashing until a safe platform/bootloader adapter
  is installed; identity and verified downloads remain usable.
* Frame workflow implemented (2026-07-22): Copter `FRAME_CLASS`/`FRAME_TYPE`, Plane
  `Q_FRAME_CLASS`/`Q_FRAME_TYPE`, and applicable Rover frame parameters are offered only
  when both a live value and writable firmware metadata exist. Selectors contain only the
  values advertised by that metadata and show current versus pending values and reboot state.
* Reviewed frame changes are written sequentially and confirmed from live parameter
  readback. Later failures trigger best-effort rollback; cancellation and incomplete rollback
  produce explicit manual-recovery guidance. Completion evidence is saved only after every
  requested value is confirmed. Initial safety recommendations are opt-in and never silently
  added to a frame write.
* Accelerometer workflow implemented (2026-07-22): Core owns a non-blocking state machine
  for six-position accelerometer and level calibration. Six-position progress follows
  ArduPilot `ACCELCAL_VEHICLE_POS` orientation/success/failure commands; level completion
  requires terminal `COMMAND_ACK`. MAVLink 2 ACK progress is retained and monotonic, while
  `STATUSTEXT` is supplemental guidance only.
* Setup shows unambiguous placement instructions with the existing calibration images,
  explicit Confirm orientation, progress, cancellation, timeout/failure, and reconnect/retry
  states. A shared per-vehicle operation lease prevents calibration and ordinary commands
  from competing. Calibration parameters are refreshed and completion evidence is recorded
  only after protocol-confirmed success.
* Compass workflow implemented (2026-07-22): compass instances are discovered from
  `COMPASS_DEV_ID*` presence (sparse-index aware) with use/external flags, board orientation,
  priority ordering, offsets, motor-compensation visibility, and aggregate 3D-magnetometer
  health. Orientation, use, and external edits are metadata-backed and readback-confirmed,
  and are kept separate from calibration. Duplicate device IDs and unranked/stale priorities
  are surfaced as warnings, and disabling the last enabled compass requires explicit
  confirmation. Onboard calibration is a Core state machine driven by ArduPilot
  `MAG_CAL_PROGRESS`/`MAG_CAL_REPORT` with per-compass progress, an acceptance step
  (`MAV_CMD_DO_ACCEPT_MAG_CAL`), cancel (`MAV_CMD_DO_CANCEL_MAG_CAL`), retry, disconnect
  recovery, and a post-calibration fitness quality summary.

* Radio and flight-mode workflows implemented (2026-07-22): RC channels are projected live
  from `VehicleState.Radio` with PWM, normalized travel, endpoints/trim, reversal, mapped
  pilot function, and stale detection. Endpoint calibration is a state machine that captures
  live extremes, validates insufficient movement/invalid ranges/throttle reversal, and writes
  `RC*_MIN/MAX/TRIM` with readback confirmation under the shared operation lease. Flight-mode
  assignment projects the six firmware PWM slots (FLTMODE/MODE per family) with the live active
  slot resolved from the mode-channel PWM, and applies confirmed slot writes. Transmitter
  configuration is never changed automatically.

* Battery workflow implemented (2026-07-22): battery monitor instances are discovered from
  `BATT*_MONITOR` presence (sparse-aware) with monitor backend, capacity, multipliers, and
  failsafe thresholds/actions surfaced only when their parameters exist. Live voltage/current/
  consumed/remaining are shown with freshness (remaining is labelled an estimate, never exact
  capacity truth). Measured-vs-reference voltage and current calibration scale
  `BATT_VOLT_MULT`/`BATT_AMP_PERVLT`; all writes are readback-confirmed with reboot flags.
  Invalid failsafe combinations (low ≤ critical, non-positive capacity/multipliers) are
  rejected before writing and surfaced as blocking issues.

* ESC/motor/servo workflow implemented (2026-07-22): motor testing is a bounded, safety-gated
  Core service driven by `MAV_CMD_DO_MOTOR_TEST` (single and sequence), always passing a
  duration timeout, blocked while armed, capped at a maximum duration/throttle, with an
  emergency stop that re-issues zero throttle and an audit log of every operation. ESC
  calibration is presented as protocol-aware guidance (analog PWM requires the manual
  all-at-once procedure; DShot does not), never a fake protocol. Servo output functions are
  projected with live PWM and reassigned with readback-confirmed writes. Motor testing is
  only exposed for rotorcraft-style families.

* Optional-hardware workflow implemented (2026-07-22): peripherals are discovered through a
  plugin-style module catalog (`IOptionalHardwareModule` registered via DI, no central switch),
  each with a parameter-presence availability predicate. First-wave modules cover serial ports
  (with exclusive-protocol conflict detection), GPS/GNSS (cross-version parameter names),
  sparse rangefinder instances, airspeed, and CAN. Only settings belonging to an available
  module can be written; edits are metadata-backed, readback-confirmed, and aggregate a
  reboot-required banner. Sensitive values are marked and never logged.

* Safety and summary workflows implemented (2026-07-22): an evidence-based safety assessment
  reports arming checks, the hardware safety switch, radio/battery/GCS/EKF failsafes, and the
  geofence with per-item Pass/Warning/NotConfigured/Unsupported/NotAssessed status and
  contradiction warnings (unsupported checks are never shown as passed). A consolidated setup
  summary aggregates identity, firmware, frame, per-workflow completion evidence, and safety,
  exportable as indented JSON and readable Markdown, explicitly stating it is not a
  safe-to-fly certification.

### Missing (v1.38 feature inventory)

* **Install Firmware**: manifest-driven firmware download/flash (stable/beta/custom), board detection, bootloader handling; "Wizard" guided first-time setup
* **Optional Hardware**: Battery Monitor (analog/smart), CompassMot, Range Finder/Sonar, Airspeed sensor, Optical Flow / PX4Flow, OSD, Camera Gimbal (Mount), Motor Test, Bluetooth, SiK Radio configuration, Antenna Tracker, Parachute, ADSB, DroneCAN/UAVCAN, Serial port mapping, GPS ordering, ESP8266, CubeID, Joystick

Note: accelerometer/level, compass, and radio calibration plus flight-mode assignment are
implemented (2026-07-22). The remaining interactive calibration flow (ESC) still requires
MAVLink command sequences that the domain does not implement yet.



# CONFIG

The Config screen replaces v1.38's Config/Tuning (`SoftwareConfig.cs`). New UI:
`Views/ConfigTuning` — tab views exist for GeoFence, Basic Tuning, Extended Tuning,
Onboard OSD, MAVFtp, Full Parameters List, Planner and CubeLan8PortSwitch. Full Parameters
List, MAVFTP, GeoFence, Basic Tuning, Extended Tuning, Onboard OSD, Planner, and the
repository-verified CubeLAN subset are implemented.

All parameter-editing Config pages share a vehicle-and-firmware-scoped editing session.
It separates original, live, and pending values; validates metadata ranges, increments,
enums, bitmasks, and read-only flags; confirms grouped writes by registry readback; keeps
partial failures retryable; aggregates reboot requirements; and blocks stale writes after
disconnect, vehicle switch, or firmware change. Navigation within Config preserves edits,
while leaving Config warns before discarding them.

## Full Parameters List

### Status

* Loads metadata and parameters and merges these (see PARAMETERS.md)
* Uses the shared Config editing session for search, validation, pending values, grouped
  apply/revert, per-field write status, confirmed readback, and reboot warnings
* Loads and saves `.param`/JSON files using invariant numeric formatting
* Retains the packed MAVFTP download with automatic classic-stream fallback

### Missing

* Parameter file compare/diff
* Favorites and modified-only views (v1.38 raw params features)

### Issues

* Parameter loading is way too slow (profile the metadata/value merge)

## GeoFence

### Status

* Resolves fence enable, type, action, altitude, radius, margin, return, auto-enable, and
  option parameters from live metadata through the shared Config editing session
* Downloads, uploads, and clears dedicated `MAV_MISSION_TYPE_FENCE` geometry with
  acknowledged retry behavior and item-level progress
* Edits inclusion/exclusion polygons and circles plus the return point on a dedicated map;
  fence geometry is not added to the flight mission aggregate
* Rejects undersized, open, self-intersecting, non-finite, or over-limit geometry and
  invalid radius/altitude relationships before any vehicle change
* Distinguishes local and synchronized vehicle revisions, preserves a backup before
  replace/clear, and keeps local work recoverable after disconnects or partial downloads

### Limitations

* Typed geometry requires the vehicle's fence mission protocol; parameter-only fence
  configuration remains available when that protocol is not supported
* Physical-vehicle/SITL validation is still required for firmware-specific option values

## Basic Tuning

### Status

* Selects separate curated profiles for ArduCopter, ArduPlane, Rover, and ArduSub; only
  fields present in the connected vehicle's registry are shown
* Groups pilot feel/responsiveness, climb and descent, navigation, cruise/throttle,
  loiter/turn behavior, and acceleration as appropriate to each firmware family
* Uses firmware metadata for range sliders, enums, read-only state, increments, live and
  pending values, while curated descriptions and unit fallbacks keep every field explained
* Applies, refreshes, and reverts one group at a time through the shared confirmed-readback
  editing session; coupled ordering and speed/acceleration rules block unsafe combinations
* Imports and exports invariant JSON containing only the parameters presented by the active
  profile; imports are atomic and unsupported names are reported and ignored
* Hides expert loop-gain fields and displays explicit control-stability warnings before
  applying risk-bearing groups

### Limitations

* No static defaults are presented because the repository has no authoritative,
  vehicle/version-specific recommendation source; metadata and live vehicle values remain
  the source of truth
* Physical flight/drive/dive testing remains essential after tuning changes

## Extended Tuning

### Status

* Selects structured Copter, Plane, Rover, and Sub advanced profiles covering rate/attitude
  controllers, position/velocity/depth control, feed-forward, TECS/L1, steering/speed,
  estimator noise, filters/notches, and autotune configuration where parameters are present
* Generates repeated axis and gyro-instance editors from descriptors and keeps editor rows
  lazy inside a virtualized group collection; a 10,000-field synthetic expansion is covered
  by the performance test
* Searches only the curated advanced set, validates metadata plus PID integrator and notch
  relationships, and shows normalized pending magnitudes across controller axes
* Requires a non-mutating source/target preview and a separate confirmation before an axis
  copy enters pending state; vehicle writes still require the group's expert review prompt
* Shows a complete pending change summary and explicit stability warnings before confirmed
  group writes through the shared Config editing session
* Displays latest `PID_TUNING` desired/achieved/error values as read-only context; it does
  not start or orchestrate autotune

### Limitations

* Live response context appears only when firmware is already emitting `PID_TUNING`
* The page is a curated expert surface, not a replacement for Full Parameters List

## Onboard OSD

### Status

* Discovers numbered screens, built-in/custom item stems, enable/X/Y fields, and additional
  option/unit/warning parameters from live `OSD<n>_*` names and metadata
* Derives character-grid dimensions from coordinate metadata and renders enabled items in a
  MAUI `GraphicsView`; selected items can be placed by exact numeric row/column or accessible
  directional controls
* Validates whole-number bounds and detects same-cell overlaps. Static overlaps block apply;
  metadata-advertised dynamic overlaps require an explicit warning confirmation
* Exposes discovered screen enable/options/resolution fields and firmware-global OSD options
  through shared metadata-backed editors
* Applies only the selected screen through the shared confirmed-readback session and supports
  reset-to-live plus family-tagged, atomic JSON import/export of discovered parameters
* Does not expose a font upload action because no font-transfer service currently exists

### Limitations

* The preview is a character-grid placement model, not a pixel-perfect emulation of every
  video backend, font, or firmware-dynamic item width
* Physical OSD hardware validation is still required after layout changes

## Planner application preferences

### Status

* Edits local, typed settings for units, map source/style/default zoom, telemetry display
  rates, theme, logging and retention, connection defaults, parameter-cache policy,
  confirmations, update checks, and telemetry accessibility
* Persists a versioned JSON document through MAUI Preferences, migrates older schemas, and
  replaces corrupt or invalid documents with validated safe defaults
* Applies theme previews immediately and publishes all saved changes to observable runtime
  consumers; disconnected connection defaults and new-map defaults consume the live snapshot
* Marks map tile-source/style, logging, and update-channel changes as restart-required
* Resets individual sections or all settings and supports JSON import/export; unknown secret
  fields are ignored and exports contain only the typed non-secret model
* Keeps credentials and tokens behind the platform SecureStorage abstraction and never in
  ordinary preferences, exports, or structured settings logs

See [PLANNER_SETTINGS.md](PLANNER_SETTINGS.md) for the settings inventory and persistence
contract.

### Limitations

* Telemetry refresh, cache, confirmation, update, and accessibility settings are observable
  application policy; individual feature consumers may adopt them incrementally
* Map source/style changes are intentionally applied when map views are recreated

## CubeLAN 8-port switch

### Status

* Discovers and reads the switch through the repository-documented MAVLink `DEVICE_OP`
  I²C path (bus 0, address `0x50`) and reports disconnected, not discovered, unsupported,
  and available states explicitly
* Presents exactly eight ports and the verified class-of-service enable/priority, Energy
  Efficient Ethernet, tagged VLAN, and 8-by-8 VLAN membership bits
* Requires a confirmed read before editing, tracks local changes, writes only changed
  bytes, reads every write back, and performs a full post-apply confirmation
* Attempts and confirms rollback to the original snapshot after a failed apply
* Exports only verified settings; authentication secrets and preserved unknown registers
  are excluded
* Uses a generic typed vendor-device boundary for identity, transport, capabilities,
  validation, snapshots, apply/rollback results, and reboot/reconnect state

### Limitations

* PoE, port enablement, modes, VLAN IDs, authentication, editable labels, and reboot or
  reconnect commands are not exposed because no verified protocol definition exists in
  the repository
* Physical CubeLAN and ArduPilot validation is still required; the protocol evidence and
  real-hardware checklist are documented in [CUBELAN.md](CUBELAN.md)

## Other Config capabilities (Missing)

Remaining v1.38 feature inventory:

* **Flight Modes**: assign flight modes to RC switch positions (not present as a tab yet)
* **Standard / Advanced Params**: "friendly" curated parameter lists with combos/sliders
* **Full Parameter Tree**: tree-grouped variant of the raw parameter editor
* **MAVFTP**: remote filesystem browser, navigation, session reset, streaming download, progress, cancellation, burst recovery; SITL re-validation pending after endpoint/sequence interoperability fixes
* v1.38 extras to consider later: FFT analysis, REPL/Terminal, DroneCAN tooling



# SIMULATION

Replaces v1.38's Simulation screen (SITL). New UI: `SimulationView`.

### Status

* Implemented (2026-07-23): persisted simulator profile workspace covering firmware family,
  frame/model, location, speedup, named ports, binary/version, tokenized arguments, and
  environment
* Process/container/remote-neutral Core runtime and owned-session contracts; explicit
  Stopped, Validating, Starting, WaitingForHeartbeat, Running, Stopping, Completed, and
  Failed states
* Start/stop/restart controls, bounded live stdout/stderr, elapsed time, exact runtime/PID
  identity, connection endpoints, structured lifecycle logging, and redacted diagnostics
  export
* Pre-start validation covers values, port duplication/conflicts, paths, host executable
  permission, and runtime-specific incompatibility
* Navigation preserves an owned session; app shutdown performs bounded cleanup of that
  exact session and never kills by process name
* Verified SITL installation management for configured external binaries and versioned
  application-owned caches: exact family/channel/platform/architecture selection, progress
  and cancellation, SHA-256 verification, traversal-safe atomic extraction, exact profile
  pins, and retention that cannot delete external installations
* Host capability and version probing are behind platform services for Windows, Linux,
  WSL-hosted applications, and macOS; unsupported application platforms fail explicitly
* Direct ArduPilot SITL launch for Copter, Plane, Rover, and Sub with conservative frame
  catalogs; typed model/home/speedup/instance/SystemId/defaults/serial arguments; exact
  endpoint leases; isolated working/log directories; and optional visible console
* Automatic connection reuses the existing UDP vehicle stack and verifies heartbeat
  SystemId plus firmware family. Process-running and vehicle-connected remain distinct;
  startup stderr and heartbeat failures are actionable
* Connection generations make simulator cleanup ownership-safe: disconnect the exact
  connection first, then the exact process tree and endpoint lease; never replace an
  existing hardware connection silently
* Typed start location supports built-in presets, latitude/longitude/altitude/heading, and
  map-click selection. Documented runtime wind, GPS, compass, RC, and temporary battery
  controls are discovered by live firmware parameter presence and expose unavailable
  controls with an explanation
* Hazardous simulation controls require confirmation and bounded duration, confirm MAVLink
  parameter readback, automatically restore a safe value, target the exact session and
  `VehicleId`, and retain simulation-time audit events. Versioned scenario presets remain
  separate from launch profiles
* Closed, versioned JSON scenario runner supports bounded state/telemetry waits, mode, arm,
  takeoff, embedded mission upload/start, documented fault injection/reset, land, and
  telemetry assertions. It provides exact-target dry runs, safe-boundary pause/resume,
  cancellation cleanup, and machine-readable/readable evidence reports without arbitrary
  script execution
* Multi-instance orchestration deterministically allocates instance numbers, SystemIds,
  primary and component ports, home offsets, and per-instance runtime/telemetry/DataFlash/
  cache directories. Start-all and stop-all use bounded concurrency and retain per-session
  failures, process state, exact vehicle identity, connection state, and active selection
* Concurrent SITL connections own independent UDP/client/parameter sessions while sharing
  one inbound dispatcher. Commands, mission transfers, runtime controls, and scenarios route
  by the exact session/`VehicleId`; disconnect and crash cleanup remove only that vehicle
* Owned-process markers include PID, executable path, and operating-system start time.
  Unclean-exit recovery terminates only an exact identity match and leaves PID-reuse or
  uninspectable processes untouched for operator review
* Indexed `.tlog` playback with pause, seek, speed, deterministic replay clock, and state
  reconstruction through an isolated read-only decoder/vehicle pipeline. A connection-level
  transmission policy blocks all outbound MAVLink while replay is loaded
* Diagnostic schema v2 exports redacted tokenized command lines, component/runtime versions,
  ports, process/session state, heartbeat readiness, bounded recent output, artifact paths,
  and replay statistics. Simulation tests are split into deterministic unit, fake-runtime,
  and explicit opt-in real-SITL tiers with hard startup/step/total/cleanup bounds
* Four opt-in real-SITL family smoke tests use strict total and cleanup timeouts and skip
  explicitly when their configured binaries are absent
* Asset: `src/Tests/MissionPlanner.Simulator` already hosts a simulator used by the smoke
  tests — a candidate backend for an in-app SITL experience

See [SIMULATION.md](SIMULATION.md) for architecture, persistence, diagnostics, and the
current verification boundary.

### Missing (v1.38 feature inventory)

* Select vehicle model (multirotor, heli, plane, quadplane, rover, sub)
* Ship/configure the production official ArduPilot artifact manifest (the acquisition
  pipeline exists but the repository intentionally does not invent an endpoint)
* Autonomous swarm control (multi-vehicle launch offsets are data only by design)
* Telemetry-log KML/graph export and live `.tlog` recording remain Flight Data work



# HELP

### Status

* Placeholder page only

### Missing

* Version/about info, update check (v1.38: check for updates, changelog display)



# EXIT

### Status

Completed

# MAVFTP

* General protocol: codecs, singleton dispatcher, bounded multi-packet burst reordering and gap
  recovery, normal-read fallback, streaming, retry, cancellation, cleanup, and listing implemented.
* File browser UI: listing, navigation, refresh, reset, download, progress, save, and cancellation implemented.
* Upload and filesystem mutations: not implemented.
* Fast packed-parameter download: implemented through `@PARAM/param.pck`; decoding or
  transfer failure falls back to the classic parameter stream automatically.

# MAVLINK DIALECT COVERAGE

* A deterministic inventory resolves the pinned official `ardupilotmega` dialect and its
  transitive includes without network access during normal builds or tests.
* A generated frozen registry now supplies names, CRC extras, payload bounds, dialect
  origins, and deprecation state for all 325 resolved messages.
* All 221 inherited protocol enums and command IDs are generated with XML-defined flag
  semantics and wire-field-derived storage widths; undefined future values remain round-trippable.
* Protocol-to-domain mappings keep generated identity and capability enums separate from
  `FirmwareFamily` and the vehicle domain capability subset.
* Frame validation uses the registry, including MAVLink 2 extension truncation bounds and
  signed-frame sizing; it no longer depends on a typed decoder or hand-maintained CRC switch.
* The coverage report includes registry, CRC, typed wire,
  decoder, domain-handler, and observation coverage in `artifacts/mavlink-coverage.json`.
* The vendored dialect revision, baseline counts, known legacy constant mismatch, and report
  command are documented in [MAVLINK.md](MAVLINK.md).
* Every CRC-valid selected-dialect frame now reaches consumers: registered typed decoders
  take priority and all other frames become lossless `RawMavLinkMessage` instances. Raw
  dispatch is debug-level, while IDs outside the selected dialect are trace-diagnosed and
  rejected because their CRC extra is unknown.
* Typed wire coverage now spans every one of the 315 non-deprecated selected-dialect
  messages: 287 generated protocol records/decoders (including the retained deprecated
  `HWSTATUS`, `BATTERY2`, `MISSION_ITEM`, and `MISSION_REQUEST` compatibility messages) and
  32 documented hand-written workflow overrides.
* Generated models preserve protocol field order and exact numeric/array types, decode
  byte-level MAVLink 2 extension truncation with zero defaults, and provide outbound payload
  serialization with optional extension trimming.
* Decoder DI is generated and startup-validated: exactly one effective decoder exists per
  typed schema slot, with fail-fast diagnostics for duplicate IDs, missing registry entries,
  undeclared overrides, coverage gaps, or stale CRC/payload metadata. No runtime assembly
  scanning or configurator decoder array is used.
* All 325 messages have a machine-readable domain-promotion category, single owner,
  frequency, stale-data decision, and intended consumers. Diagnostic/raw remains the default;
  generation does not create aggregate handlers automatically.
* Core flight-display telemetry is promoted through cohesive flight, navigation, power,
  radio, health, and control handlers. This includes quaternion attitude, GPS2, landed/VTOL
  state, primary and secondary batteries, controller/sensor health, both radio-status
  variants, and raw servo outputs while preserving the existing HUD-facing primary fields.
* Protocol sentinels are normalized to nullable domain values, scaled units are converted
  once at the handler boundary, and GPS, motion, flight, power, health, and radio state expose
  timestamp-based stale checks. Repeated identical samples no longer publish redundant
  `VehicleStateUpdated` events from the cohesive telemetry handlers.
* Navigation-quality and low-rate sensor promotion now adds coherent estimator, vibration,
  multi-instance pressure, keyed range, environment, and vehicle-time state. AHRS/AHRS3,
  vibration, three barometers, distance sensors, terrain, wind/covariance, altitude, and
  system time use focused observations and change-gated state publication.
* High-rate raw/scaled/high-resolution IMU, obstacle arrays, optical flow, and odometry stay
  typed and inspector/log accessible but deliberately bypass immutable aggregate events to
  avoid allocation and EventHub pressure.
* Command acknowledgement correlation is connection-wide and keyed by vehicle and command;
  transient command services do not own or dispose the shared MAVLink connection. Action
  commands are safety-gated in Core, serialized per vehicle, and keep `IN_PROGRESS`
  correlations alive until a terminal acknowledgement.
* The mission transfer state machine filters source vehicle and mission type, buffers early
  and out-of-order INT items, ignores duplicates, and retains typed legacy float mission
  compatibility. The pinned source revision has no `MISSION_CHANGED` schema.
* Packed MAVFTP parameters are preferred with classic parameter streaming as an automatic
  fallback. Log, camera, gimbal, ADS-B, generator/EFI, ESC, winch, landing-target,
  OpenDroneID, cellular/Wi-Fi, CAN, serial/tunnel, and device-operation families have typed
  protocol access and inspector visibility; product services are added only when required.
* Independent pymavlink fixtures cover all 287 generated decoders, including MAVLink 1 and 2
  stream cases, without adding a Python runtime dependency.
* A committed generation manifest and `Generate-MavLinkDialect.ps1` provide offline,
  deterministic verify/write workflows. A dedicated GitHub Actions gate detects manual or
  stale generated output, reports all 325 coverage classifications, and runs registry,
  decoder, raw-fallback, promotion, and conformance tests.





