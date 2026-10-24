# MissionPlanner Next Generation — missing features

Baseline date: 2026-10-23. For Mandatory Hardware, Optional Hardware, Config and Advanced, the newer
[2026-10-24 implementation ledger](review-20261023/IMPLEMENTATION.md) takes precedence over this baseline.
Other areas below were not re-audited by that implementation. This baseline superseded the older "Missing" sections in
[FEATURES.md](FEATURES.md). Each item was checked against the code on branch `feature/PX4Flow`, or against a
living document (named in the last column). "Missing" means no implementation was found; "Partial" means a
smaller version exists. The context is in [ANALYSIS.md](ANALYSIS.md).

Impact: **H** operators notice daily, **M** useful workflow gap, **L** niche or deferred by decision.

## Flight Data and map

| Feature | State | Impact | Evidence |
|---|---|---|---|
| Guided map actions: Fly To Here, Fly To Here Alt, Fly To Coords, Point Camera Here, Trigger Camera Now | Missing | H | No code; listed in FEATURES.md |
| Live highlighting of the active mission item on the map | Missing | H | No code; MISSIONS.md "next steps" 1 |
| Flight track trail (breadcrumb) | Missing | M | No code |
| Waypoint radius circles on the map | Missing | M | No code |
| Draggable waypoint and home markers | Missing | M | MISSIONS.md "next steps" 2 |
| ADS-B aircraft on the map (traffic is listed in the Transponder tab) | Not confirmed | M | FLIGHT_DATA.md |
| HUD overlay details: EKF and vibration indicators, GPS fix text, warning banners | Partial | M | HUD shows attitude, speeds, altitude, battery, satellites, arming, pre-arm reason |
| Video: camera stream in the HUD or a video window | Missing | M | No code |
| Cursor readout (lat/lng, UTM, MGRS, terrain) and UTM/MGRS table columns in the editor | Missing | L | FEATURES.md |

## Flight Planner

| Feature | State | Impact | Evidence |
|---|---|---|---|
| Choosing any `MAV_CMD` in the editor (unknown commands are now preserved but cannot be created) | Partial | M | Mission command catalog |
| One mission-type selector driving the editor (fence and rally have their own workflows) | Partial | L | `VehiclePlanWorkflow` |
| Terrain "Verify Height" against SRTM | Missing | L | FEATURES.md |
| Capability validation against the vehicle and firmware type | Missing | M | MISSIONS.md "next steps" 5 |
| Incremental row updates instead of rebuilding all rows on each edit | Missing | L | MISSIONS.md "next steps" 10 |
| Swarm control | Missing | L | External-GPS Follow Me is now implemented separately; implementation ledger AD-07 |

## Logs and analysis

| Feature | State | Impact | Evidence |
|---|---|---|---|
| Dataflash (BIN) log download, browsing and graphing | Missing; the Dataflash view is a placeholder | H | `DataFlashLogsViewModel` is a stub |
| Graphing and review of telemetry logs; KML export of a flight | Missing | M | FEATURES.md |
| Automatic log analysis | Missing | L | No code |

## Setup

| Feature | State | Impact | Evidence |
|---|---|---|---|
| PX4Flow page in Optional Hardware | In progress (branch `feature/PX4Flow`) | M | incomplete-tasks.txt |
| Serial Ports: detected RC protocol, frames received and link quality per port | Missing (the multiple-RCIN warning exists) | M | incomplete-tasks.txt |
| Compass page: decode `COMPASS_DEV_ID` into chip, bus and address (the Hardware ID page already decodes) | Partial | L | incomplete-tasks.txt |
| CubeLAN: PoE, port modes, VLANs, authentication | Missing; no verified protocol definition | L | CUBELAN.md |

## Advanced and configuration

| Feature | State | Impact | Evidence |
|---|---|---|---|
| Legacy serial CLI Terminal | Intentionally retired | L | Implementation ledger AD-13; onboard scripting is a separate workflow |
| Script REPL | Implemented; hardware qualification pending | L | Implementation ledger AD-12 |
| User Params (user-defined parameter collections) | Implemented in Parameters Editor | M | Implementation ledger CF-08 |
| Plugin system | Missing | L | No code |
| About, version and application update check | Missing | M | No code |
| Localization | Postponed by decision | L | postponed-2026-10-22.md |

## Parameters

| Feature | State | Impact | Evidence |
|---|---|---|---|
| Favorites in the Parameters Editor (they exist on the Setup pages) | Missing | M | FEATURES.md |
| Comparing two parameter files directly (comparison always uses the connected vehicle) | Missing | M | FEATURES.md |
| Fast parameter loading (the metadata/value merge is slow) | Known issue, not profiled | H | FEATURES.md "Issues" |
| Richer file and profile source selection in the comparison workflow | Partial | M | tasks/20261015 |
| Splitting and tidying the Parameters Editor / Quick Editor layout | Open | M | tasks/20261015 |

## Connection

| Feature | State | Impact | Evidence |
|---|---|---|---|
| Automatic re-establishment of a lost link (the connection is monitored and the loss is reported) | Missing | H | VEHICLE_CONNECTION.md "known gaps" |
| Bluetooth and ELRS transports | Missing | L | FEATURES.md |
| Live-vehicle validation of TCP and UDP clients | Not done | M | FEATURES.md |

## Firmware

| Feature | State | Impact | Evidence |
|---|---|---|---|
| Install Firmware on Linux and macOS (the page shows an unsupported-platform notice) | Disabled | H | FIRMWARE.md; `OperatingSystem.IsWindows()` gate |
| STM32 DFU installation and device catalog outside Windows | Missing | M | FIRMWARE.md |
| Legacy firmware (AVR `.hex`, VRBrain) | Deferred | L | tasks/firmware/Deferred feature backlog.md |
| Secure firmware and secure bootloaders | Research | L | Deferred feature backlog |
| Force Bootloader | Research | L | Deferred feature backlog |
| Extra transports: UART adapter, DroneCAN, BlueOS, SD-card `.abin` | Deferred | L | Deferred feature backlog |
| Production official-manifest endpoint shipped by default | Not configured | M | FEATURES.md |

## Maps

| Feature | State | Impact | Evidence |
|---|---|---|---|
| Keyed provider presets (Bing, Mapbox, Google, regional providers); custom raster sources and MBTiles work | Missing | M | MAPS.md, FEATURES.md |
| Vector maps and PMTiles | Deferred by ADR | L | adr/ADR-0006 |
| Local GeoTIFF/GDAL rasters | Dropped by decision | L | FEATURES.md |
| Map export with attribution footer | Missing | L | MAPS.md |

## Platforms

| Feature | State | Impact | Evidence |
|---|---|---|---|
| macOS: any run, the keychain store and the app bundle | Untested | H | PLATFORMS.md |
| Linux: use with a real flight controller; blocking CI tests | Untested | H | PLATFORMS.md |
| Device location on Linux and macOS | Missing | L | PLATFORMS.md |
| Android and iOS | Not supported | L | PLATFORMS.md |

## Implemented, but not yet confirmed on hardware

Accelerometer calibration page, motor-test start thresholds, CompassMot, geofence option values, Onboard OSD
layout, CubeLAN switch, receiver binding, and the live read/write mission workflow against a vehicle.
