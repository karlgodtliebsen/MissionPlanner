# Documentation Index

This index covers the current documentation layout. Start with the architecture and
subsystem guides for implementation details, and check each document's verification date
when assessing status. Dated reviews, task notes and archived reports can describe work
that has since changed; implemented features are not necessarily qualified on hardware.

The original Mission Planner source is on
[GitHub](https://github.com/ArduPilot/MissionPlanner). See [LEGACY_SOURCE.md](LEGACY_SOURCE.md)
for resolving legacy paths in older tasks and preserving historical audit provenance.
The former local `src-v.1.38` tree is no longer part of this repository.

## Start here

| Document | Purpose |
|---|---|
| [Unified task review](tasks/README.md) | Single active backlog: 74 reconciled tasks and ideas, with stable IDs, evidence and Execute/Defer/Dismiss decisions |
| [DESIGN_CONCEPTS.md](DESIGN_CONCEPTS.md) | Architecture, layers, domain boundaries, immutable state and UI patterns |
| [ARCHITECTURE_DECISION_RECORDS.md](ARCHITECTURE_DECISION_RECORDS.md) | Architectural conventions and decisions; individual accepted ADRs are also indexed below |
| [FEATURES.md](FEATURES.md) | Detailed feature inventory, implementation notes and limitations by area |
| [PLATFORMS.md](PLATFORMS.md) | .NET 11 build/run guidance and Windows, Linux, macOS and browser status; mobile heads are unsupported templates |
| [Test guide](../src/Tests/README.md) | Current test commands, Microsoft.Testing.Platform, headless isolation, skipped regressions and opt-in hardware tests |
| [AI.md](AI.md) and [AGENTS.md](AGENTS.md) | Contributor guidance and coding-agent working rules; the root `ai.md` redirects to `docs/AI.md` |
| [LEGACY_SOURCE.md](LEGACY_SOURCE.md) | Upstream source, deleted local legacy paths and historical version conventions |

## Build and run prerequisites

Install the **.NET 11 SDK** to build and run the project from source. The repository's
[global.json](../global.json) selects SDK **11.0.100 or a later stable 11.0 feature band**.
The SDK includes the matching runtimes; a runtime-only installation cannot build the project.

The **browser project also requires the `wasm-tools` workload** for .NET 11 WebAssembly
compilation and native linking, including Debug builds. The full solution includes the
browser project, so it requires this workload too. Desktop-only builds need no additional
.NET workloads.

After installing the SDK, run these commands from the repository root to check the selected
SDK and install the browser tools:

```powershell
dotnet --version
dotnet workload install wasm-tools
dotnet workload list
```

`dotnet --version` should report an `11.0.x` SDK, and the workload list should include
`wasm-tools`. Use the workload for the selected .NET 11 SDK, rather than a version-suffixed
workload for an older target such as `wasm-tools-net10`.

See [Building and running](PLATFORMS.md#building-and-running) for solution and desktop
commands, and the [browser run instructions](../src/Platforms/MissionPlanner.Browser/README.md#run)
for launching the WebAssembly application.

## Directory structure

| Location | Contents and status |
|---|---|
| `docs/*.md` | Architecture, subsystem guides, operational notes and dated analysis; grouped below |
| [adr/](adr/) | Individual architecture decision records |
| [tasks/README.md](tasks/README.md) | Unified review list for outstanding work, qualification and future ideas; replaces the former mixed task notes |
| [review-20261023/](review-20261023/) | Original and Next Gen screenshots, capture manifest, feature comparison and the subsequent implementation ledger |
| [images/theme-palettes/](images/theme-palettes/) | Palette previews used by the theme documentation |
| [Archived/](Archived/) | Historical reviews, audits, migration guides and task sets; retained for provenance |
| [Archived/tasks/](Archived/tasks/README.md) | Source reconciliation ledger, historical implementation instructions and execution reports; remaining work maps to unified task IDs |

Documentation tied closely to source also lives outside `docs/`: the
[test guide](../src/Tests/README.md), [browser build notes](../src/Platforms/MissionPlanner.Browser/README.md),
[MAVLink dialect provenance](../src/Core/MissionPlanner.MavLink/Dialects/README.md), and
[virtualized grid sample](../src/UI/MissionPlanner.App/Views/Samples/VirtualizedItemsGrid/README.md).

## Subsystem references

### Connections and protocols

| Document | Purpose |
|---|---|
| [VEHICLE_CONNECTION.md](VEHICLE_CONNECTION.md) | Connection ownership, resilience, telemetry streams, cleanup and test practices |
| [NETWORK_TRANSPORT_IMPLEMENTATION.md](NETWORK_TRANSPORT_IMPLEMENTATION.md) | UDP/TCP transport behavior, endpoint handling and verification |
| [MAVLINK.md](MAVLINK.md) | Pinned dialect inputs, generated wire coverage and maintenance workflow |
| [MAVLINK_DOMAIN_PROMOTION.md](MAVLINK_DOMAIN_PROMOTION.md) | Domain-promotion policy and ownership; data is in [mavlink-promotion-catalog.json](mavlink-promotion-catalog.json) |
| [MAVFTP.md](MAVFTP.md) | MAVLink filesystem operations, correlation, streaming, retries and limitations |

### Flight Data, missions and maps

| Document | Purpose |
|---|---|
| [FLIGHT_DATA.md](FLIGHT_DATA.md) | Flight Data architecture, lifecycle, HUD, tabs and operator workflows |
| [flightdata-actions-parity.md](flightdata-actions-parity.md) | Actions parity decisions, implemented capabilities and deliberate scope limits |
| [MISSIONS.md](MISSIONS.md) | Mission domain, planning, files, validation, transfer, execution monitoring and map-menu behavior |
| [MAPS.md](MAPS.md) | Map catalog, provider policy, credentials, attribution, offline packs and renderer integration |
| [READINESS_AND_DIAGNOSTIC_NAVIGATION.md](READINESS_AND_DIAGNOSTIC_NAVIGATION.md) | Structured readiness and shared diagnostic navigation, replacing the Flight Data Preflight tab |
| [LiveTelemetryInspector.md](LiveTelemetryInspector.md) | Shared Inspector drawer, panels and desktop detachment |
| [VEHICLE_SCRIPTS.md](VEHICLE_SCRIPTS.md) | Constrained declarative vehicle automation format and execution boundary |

### Parameters, configuration and hardware setup

| Document | Purpose |
|---|---|
| [PARAMETERS.md](PARAMETERS.md) | Parameter request/set, bulk streaming, editing, storage and metadata |
| [PARAMETER_PROFILES.md](PARAMETER_PROFILES.md) | Parameter profile creation, management and application |
| [PLANNER_SETTINGS.md](PLANNER_SETTINGS.md) | Local application preferences, including display and map settings |
| [SetupUxPattern.md](SetupUxPattern.md) | Shared Setup page structure, semantic status, actions and pending changes |
| [OPTIONAL_HARDWARE.md](OPTIONAL_HARDWARE.md) | Capability-driven Optional Hardware workspace and implementation/qualification notes |
| [ADVANCED_SETUP.md](ADVANCED_SETUP.md) | Advanced workflows, services and recorded scope decisions |
| [CALIBRATION.md](CALIBRATION.md) | Calibration services, state, protocol acknowledgements and UI projection |
| [ACCELEROMETER_CALIBRATION.md](ACCELEROMETER_CALIBRATION.md) | Accelerometer orientation workflow and operator guidance |
| [COMPASS_DIAGNOSTICS.md](COMPASS_DIAGNOSTICS.md) | Configured, detected, required and healthy compass evidence |
| [ARMING.md](ARMING.md) | Semantic arming setup, diagnostic authority and configuration |
| [RECEIVER_BIND.md](RECEIVER_BIND.md) | Receiver binding, command gating and ExpressLRS compatibility notes |
| [SERIAL_PORTS.md](SERIAL_PORTS.md) | Flight-controller SERIAL parameter configuration and diagnostics |
| [MOTOR_OUTPUT_DIAGNOSTICS.md](MOTOR_OUTPUT_DIAGNOSTICS.md) | Motor/output mapping and diagnostic evidence |
| [MOTOR_START_THRESHOLDS.md](MOTOR_START_THRESHOLDS.md) | Guided motor threshold workflow and operator acknowledgement |
| [CUBELAN.md](CUBELAN.md) | CubeLAN configuration, supported protocol and limits |

### Firmware and recovery

MissionPlanner's ArduPilot firmware workflow uses an immutable installation plan with
connected/disconnected safety policies, cached Stable/Beta/Latest catalogues and explicit
selection across available board targets. The installation page supports local APJ packages
and combined STM32 HEX images; the underlying package parser also supports PX4 containers.

The workflow discovers serial bootloaders across port changes, verifies compatibility and
firmware integrity, supports embedded bootloader updates through acknowledged MAVLink commands,
detects returning devices and provides copyable diagnostics. Direct hardware operations
currently require Windows. The guides below describe the architecture, supported operations
and remaining qualification.

| Document | Purpose |
|---|---|
| [FIRMWARE.md](FIRMWARE.md) | Firmware installation plans, discovery, compatibility, verification and supported workflows |
| [FirmwareIdentityAndRecovery.md](FirmwareIdentityAndRecovery.md) | Independent identity evidence, compatibility decisions and explicit recovery |
| [BETAFLIGHT_DFU_AND_ARDUPILOT_CONVERSION.md](BETAFLIGHT_DFU_AND_ARDUPILOT_CONVERSION.md) | Betaflight identity, MSP/DFU handoff, conversion and remaining qualification |

### Logging, analysis and simulation

| Document | Purpose |
|---|---|
| [Logging.md](Logging.md) | Telemetry and application logging, storage, viewers, retention and verification |
| [SERILOG_QUICKSTART.md](SERILOG_QUICKSTART.md) | Application diagnostic logging configuration |
| [TELEMETRY_RECORDING.md](TELEMETRY_RECORDING.md) | PC telemetry recording, frame capture and integrity |
| [BENCH_TELEMETRY_REPLAY.md](BENCH_TELEMETRY_REPLAY.md) | Isolated telemetry playback and the bench regression harness |
| [FftAnalysisDesign.md](FftAnalysisDesign.md) | FFT/vibration numerical domain, import/integration contracts and validation limits |
| [SIMULATION.md](SIMULATION.md) | Simulation domain, application integration and workflows |
| [SITL-parameters.md](SITL-parameters.md) | Scratch commands and connection notes for manual SITL use |

### UI, documents and appearance

| Document | Purpose |
|---|---|
| [UI_VIEW_LIFECYCLE_AND_NAVIGATION.md](UI_VIEW_LIFECYCLE_AND_NAVIGATION.md) | Avalonia view structure, activation, disposal and navigation conventions |
| [AVALONIA_DIALOG_LIFECYCLE.md](AVALONIA_DIALOG_LIFECYCLE.md) | Dialog ownership and lifecycle rules |
| [MarkdownDocuments.md](MarkdownDocuments.md) | Application information documents, safe Markdown composition and parameter listings |
| [Theming.md](Theming.md) | Avalonia theme composition, semantic resources and styles |
| [THEME_PALETTES.md](THEME_PALETTES.md) | Palette variants and preview images |
| [APPLICATION_ICON.md](APPLICATION_ICON.md) | Desktop icon assets, generation and packaging |

## Individual architecture decisions

All four records below are marked **Accepted** in their source documents.

| Record | Decision |
|---|---|
| [ADR 0001](adr/0001-own-mavlink-implementation.md) | Own the MAVLink implementation |
| [ADR 0005](adr/ADR-0005-map-source-architecture.md) | Use a catalog-driven map source architecture |
| [ADR 0006](adr/ADR-0006-defer-vector-pmtiles.md) | Defer vector/PMTiles and retain raster/MBTiles |
| [STM32 DFU ADR](adr/ADR-DFU-STM32CubeProgrammer-CLI.md) | Use an installed STM32CubeProgrammer CLI for initial DFU support |

## Reviews, status and verification

These documents have different scopes and dates. Use their recorded evidence alongside
the subsystem guides; a historical gap list or passing automated test is not a statement
that every current workflow has been tested on a physical vehicle.

| Document | Scope and status |
|---|---|
| [ANALYSIS.md](ANALYSIS.md) | Architecture, coverage, quality and risk assessment dated 23 October 2026 |
| [MISSING_FEATURES.md](MISSING_FEATURES.md) | Historical gap baseline from 23 October 2026; current review candidates are reconciled into the unified backlog, and the implementation ledger supersedes the four audited setup/configuration areas |
| [Feature comparison](review-20261023/FEATURE_COMPARISON.md) | Pre-implementation parity review dated 24 October 2026, covering 47 groups across those four areas |
| [Implementation ledger](review-20261023/IMPLEMENTATION.md) | Follow-up of 24 October 2026: 44 groups implemented and three explicit scope decisions, with automated results and remaining hardware qualification |
| [Capture manifest](review-20261023/capture-manifest.json) | Screenshot provenance for the comparison; the folder contains both original Mission Planner and Next Gen captures |
| [KNOWN_TEST_FAILURES.md](KNOWN_TEST_FAILURES.md) | Historical failure signatures, last verified 22 October 2026 on .NET 10; use the [test guide](../src/Tests/README.md) for current commands, isolation fixes and skip inventory |
| [MAPS_PLATFORM_VERIFICATION.md](MAPS_PLATFORM_VERIFICATION.md) | Windows map verification checklist with pending interactive checks; its older platform assumptions should be read alongside [PLATFORMS.md](PLATFORMS.md) |

## Tasks and ideas to review

Use the [unified task review](tasks/README.md) as the **single active backlog**. Reconciled
on 22 November 2026, it contains 74 items with stable IDs, completion criteria, source links
and a decision field. Mark each item Execute, Defer or Dismiss; completed work retains its
ID and verification evidence. The cleanup itself does not select features for execution.

The list distinguishes concrete gaps, pending qualification, older requests requiring a
recheck, optional ideas and scope decisions. It separates implemented features from missing
hardware evidence and avoids reopening completed work such as profiles, PX4Flow, Windows
DFU, simulation infrastructure and mission map-menu tools.

The [source reconciliation ledger](Archived/tasks/README.md) maps 303 original task/idea
documents to the unified list or to completed/superseded work. The six former current notes
and the root INAV proposal are preserved unchanged under
[backlog-sources-2026-11-22/](Archived/tasks/backlog-sources-2026-11-22/). Existing architecture
decisions remain in force; the backlog identifies where a decision could be revisited.

## Archive and moved material

[Archived/](Archived/) contains the older upstream/diagnostic reviews, DI audit, button
inventory, firmware ViewModel notes, migration guides and recovery reports.
[Archived/tasks/](Archived/tasks/README.md) holds the moved task sets and execution reports for
firmware, connections, setup, maps, MAVLink, Flight Data, simulation, theming and other work.
The reconciliation ledger records completed/superseded requests and maps residual work to
the active backlog. An archived location alone does not establish completion, and historical
task instructions do not override current subsystem behavior or accepted decisions.

Older task links that begin with `docs/tasks/` may now resolve under `docs/Archived/tasks/`.
The separate [review-20261023/](review-20261023/) directory remains outside the archive and
contains both comparison evidence and the follow-up implementation ledger.

Earlier HUD integration, parameter-streaming and connection fix logs were consolidated into
the subsystem guides and deleted. Use Git history for deleted material and
[LEGACY_SOURCE.md](LEGACY_SOURCE.md) for historical source paths.
