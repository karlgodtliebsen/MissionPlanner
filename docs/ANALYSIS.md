# MissionPlanner Next Generation — analysis summary

Date: 2026-10-23. Basis: the source tree on branch `feature/PX4Flow`, the living documents in `docs/`, the
2026-10-22 improvement review, builds and test runs on Windows and Debian 13 (WSL), and the page-by-page
screenshots of the original Mission Planner in `review-20261023/`. Anything not checked against code or a
test run is marked as such. The gaps are listed in [MISSING_FEATURES.md](MISSING_FEATURES.md).

## What it is

MissionPlanner Next Generation is a clean re-implementation of the ArduPilot Mission Planner ground control
station. It is a desktop application: **.NET 10 and Avalonia 12** (Semi.Avalonia and Ursa controls,
CommunityToolkit.Mvvm, Mapsui maps, ScottPlot plots). It is built around a typed domain and an owned MAVLink
stack, and the old WinForms source is used only as a behavioral reference.

## Structure

| Project | Role | Hand-written C# lines |
|---|---|---|
| `Core/MissionPlanner.Core` | Domain, services, vehicle state, missions, setup workflows, diagnostics, replay | about 45,700 |
| `Core/MissionPlanner.MavLink` | Owned MAVLink implementation: framing, generated dialect, parameters, missions, MAVFTP, signing | about 9,400 (plus generated code) |
| `Core/MissionPlanner.Firmware` | Firmware catalog, APJ, ArduPilot bootloader, STM32 DFU, Betaflight identity, recovery | about 10,300 |
| `Core/MissionPlanner.Simulation` | SITL management and scenario runner | about 5,500 |
| `Core/MissionPlanner.Maps` | Map catalog, policy, attribution, offline packs, terrain | about 3,700 |
| `Core/MissionPlanner.Transport`, `Analysis` | Serial/UDP/TCP/WebSocket transports; FFT and vibration analysis | about 1,800 |
| `Libraries/MissionPlanner.Library` | Event hubs, factories, logging, math | — |
| `UI/MissionPlanner.App`, `Themes` | Views, view models, navigation, dialogs, theme tokens | about 58,000 |
| `UI/MissionPlanner.Library.{Desktop,Windows,Linux,MacOs,Browser}` | Platform adapters (see [PLATFORMS.md](PLATFORMS.md)) | — |
| `Platforms/*` | Application heads: Desktop (Windows), Linux, MacOs, Browser, BrowserBridge | — |

The Core projects have no UI dependency. The UI layer talks only to services and domain events.

## Architecture strengths

- **Owned MAVLink stack** ([ADR 0001](adr/0001-own-mavlink-implementation.md)): messages are generated from the official XML
  dialect, and only vehicle state that matters is promoted into the domain
  ([MAVLINK_DOMAIN_PROMOTION.md](MAVLINK_DOMAIN_PROMOTION.md)).
- **Immutable vehicle state and event hubs**: state changes flow through domain events, not shared mutable
  objects ([ARCHITECTURE_DECISION_RECORDS.md](ARCHITECTURE_DECISION_RECORDS.md)).
- **Safety by construction**: commands go through a command policy, a per-vehicle operation gate and an ACK
  tracker. Rejections now carry the vehicle's own explanation (STATUSTEXT). Destructive workflows (firmware,
  motor tests, calibration) require confirmations and report only what was actually confirmed.
- **Fidelity to what the vehicle reports**: acknowledgements are not treated as proof of effect; readiness,
  logging and compass state distinguish *configured*, *detected* and *required*.
- **Testability**: simulator and fake-vehicle based tests, a telemetry replay harness, and hardware tests that
  are opt-in and skipped by default.
- **Theming and UX consistency**: theme tokens, a shared setup-page pattern ([SetupUxPattern.md](SetupUxPattern.md))
  and shared dialogs.

## Feature coverage

| Area | Pages | State |
|---|---|---|
| Flight Data | HUD, map, Quick, Actions, Aux Function, Gauges, Messages, Payload Control, Preflight, Scripts, Servo/Relay, Status, Transponder (ADS-B) | Broad; the HUD overlay and the map's guided actions are thinner than the original |
| Flight Planner | Mission editor shared with the Flight Data map: waypoints, loiter, land, takeoff, speed, jump, ROI, spline, polygons, survey/grid, circle and text patterns, rally, geofence, POI, KML/KMZ/SHP import, elevation profile, prefetch | Broad; unknown commands are preserved but not creatable |
| Setup → Install Firmware | ArduPilot APJ over the serial bootloader, STM32 DFU, Betaflight identity and conversion, recovery | Windows only; unsupported-platform notice elsewhere |
| Setup → Mandatory Hardware | Frame, Accelerometer, Compass, Radio, Flight modes, Failsafe, ESC/Motor, Servo output, Serial ports, Hardware ID, Initial tune, ADS-B, Safety, Summary | Complete for the original pages |
| Setup → Optional Hardware | RTK/GPS inject, SiK, DroneCAN, Joystick, Battery, CAN GPS order, CompassMot, Rangefinder, Airspeed, Optical flow, OSD, Camera/Gimbal, Motor test, Bluetooth, Parachute, ESP8266, CubeID, Antenna tracker, FFT, Serial ports | 20 tabs; PX4Flow is being added |
| Setup → Arming, Advanced | Arming checks; NMEA output, mirror, MAVLink signing, proximity, inspector, warnings | Present |
| Configuration | GeoFence, Basic and Extended tuning, Onboard OSD, MAVFTP, Parameters Editor (editor, text editor, profiles, comparison), CubeLAN switch | Present; see the parameter gaps |
| Logs, Preferences, Simulation, Help | Telemetry recordings (.tlog) and replay, application logs, planner settings, SITL management and scenarios, Introduction and help | Present |

The original Mission Planner pages with no Next Gen counterpart are the **Terminal**, the **Script REPL**, the
**User Params** page and the **log browser/grapher**.

## Platforms

Windows is the supported and tested platform. Linux builds, passes the platform-neutral test suites (1,492 of
1,493 on Debian 13; the one failure is a WSL loopback limitation) and ran for 25 seconds under WSLg, but has not
been used with a flight controller. macOS compiles and publishes but has never been run. Firmware installation
and DFU remain Windows-only. The Browser head supports UDP through a local bridge. Android and iOS are not
supported. Details are in [PLATFORMS.md](PLATFORMS.md).

## Quality

- About 2,060 automated tests in 7 suites: Core 1,024 (8 skipped), UI 535, Firmware 360, Analysis 64, Tests 55,
  Smoke/Simulator 30 (22 skipped, opt-in simulator and device tests) and 1 browser-bridge test.
- Two known UI test failures that appear only in a full run ([KNOWN_TEST_FAILURES.md](KNOWN_TEST_FAILURES.md)).
- Async hygiene is enforced by analyzers. Every public type and member had XML documentation when checked
  with the `CS1591` warning on 2026-10-22 (the warning is still suppressed in `Directory.Build.props`).
- CI builds Windows, WASM, Linux and macOS; the Linux unit-test step is non-blocking until it has been stable.
- A review of the UI's event subscriptions found no leaks, and no `async void` methods remain.

## Risks and observations

1. **Hardware validation is the main gap.** Calibration, motor tests, firmware flashing, CubeLAN, OSD and
   geofence behavior are covered by simulators and fakes; much of it has not been confirmed on a vehicle.
2. **Platform reach.** Firmware installation, DFU, USB topology and location are Windows-only; Linux and macOS
   are new and unverified with hardware.
3. **Slow parameter loading** (metadata/value merge) is a known, unprofiled issue.
4. **Documentation drift.** [FEATURES.md](FEATURES.md) still has older "Missing" lists in some sections
   (for example the right-click menu stubs and map providers). [MISSING_FEATURES.md](MISSING_FEATURES.md) is the
   current list and takes precedence.
5. **Size of a few classes** (for example `SimulationViewModel`, `PreferencesViewModel`, `InstallFirmwareViewModel`
   at 1,200–1,500 lines) is the next maintainability concern after the `MissionMapViewModel` split.

## 2026-10-22 improvement review

Serialized mission transfers; lossless handling of unsupported mission commands; STATUSTEXT on rejected
commands; JSON-backed simulation stores; removal of all `async void`; async analyzers; CI repair; XML
documentation for 839 public members; Linux and macOS support; the `MissionMapViewModel` split from 2,457 to
914 lines. Postponed by decision: warnings-as-errors beyond `MissionPlanner.Firmware`, localization, and a
Core assembly split.
