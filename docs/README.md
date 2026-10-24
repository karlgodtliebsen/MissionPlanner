# Documentation Index

The new application lives in `src/`;

The original Mission Planner source is on
[GitHub](https://github.com/ArduPilot/MissionPlanner); the local `src-v.1.38` tree
has been deleted. See [LEGACY_SOURCE.md](LEGACY_SOURCE.md) for how to resolve
legacy paths in older tasks and preserve historical audit provenance.

Documentation lives in `docs/` and is organized into core documents, subsystem references, analysis and open work, and an archive.


## Core documents (keep these current)

| Document | Purpose |
|---|---|
| [DESIGN_CONCEPTS.md](DESIGN_CONCEPTS.md) | Architecture philosophy: layers, DDD, pipeline, immutable state, UI patterns |
| [ARCHITECTURE_DECISION_RECORDS.md](ARCHITECTURE_DECISION_RECORDS.md) | ADRs, naming conventions, data flow, future direction |
| [FEATURES.md](FEATURES.md) | Feature status per area (domain, UI screens): implemented vs missing |
| [LEGACY_SOURCE.md](LEGACY_SOURCE.md) | Upstream legacy source and historical path/version conventions |
| [MAPS.md](MAPS.md) | Map catalog, policy, attribution, offline, and renderer architecture |
| [PLATFORMS.md](PLATFORMS.md) | Windows, Linux, macOS and browser heads: how to build, run and what differs; mobile is not supported |
| [MAPS_PLATFORM_VERIFICATION.md](MAPS_PLATFORM_VERIFICATION.md) | Manual verification matrix for the current Avalonia desktop target |
| [MISSIONS.md](MISSIONS.md#mission-map-menu-feature-guide) | Mission-map menu ownership, safety, formats, and known limitations |

## Subsystem references

| Document | Purpose |
|---|---|
| [VEHICLE_CONNECTION.md](VEHICLE_CONNECTION.md) | Connection model, transports & resilience, telemetry streams, cleanup, test practices |
| [MISSIONS.md](MISSIONS.md) | Mission domain, planner UI, files, validation, upload/download, execution monitoring |
| [PARAMETERS.md](PARAMETERS.md) | Parameter request/set, bulk streaming, storage, metadata system |
| [FftAnalysisDesign.md](FftAnalysisDesign.md) | FFT audit, standalone frequency/vibration domain, numerical contracts and review boundary |
| [MAVLINK.md](MAVLINK.md) | Official dialect provenance, generated coverage, and maintenance workflow |
| [MAVLINK_DOMAIN_PROMOTION.md](MAVLINK_DOMAIN_PROMOTION.md) | Rules and machine-readable ownership catalog for domain promotion |
| [MAVFTP.md](MAVFTP.md) | MAVLink filesystem streaming, correlation, retries, and limitations |
| [FLIGHT_DATA.md](FLIGHT_DATA.md) | Flight Data tab architecture, lifecycle, safety, and operator workflows |
| [VEHICLE_SCRIPTS.md](VEHICLE_SCRIPTS.md) | Constrained declarative vehicle automation format and safety boundary |
| [Logging.md](Logging.md) | Telemetry recordings, application diagnostics, storage, viewers, retention, and verification |
| [SERILOG_QUICKSTART.md](SERILOG_QUICKSTART.md) | Logging setup and diagnostic workflow |
| [UI_VIEW_LIFECYCLE_AND_NAVIGATION.md](UI_VIEW_LIFECYCLE_AND_NAVIGATION.md) | Coding guide for Avalonia view lifecycle and Ursa drawer/navigation pages |

Historical point-in-time analysis and fix logs (HUD integration analysis, parameter
streaming debugging iterations, connection fix reports, etc.) were consolidated into the
documents above in July 2026 and deleted; recover them from git history if needed.

## Analysis and open work

| Document | Purpose |
|---|---|
| [ANALYSIS.md](ANALYSIS.md) | Summary analysis of MissionPlanner Next Generation: structure, architecture, coverage, quality, risks |
| [MISSING_FEATURES.md](MISSING_FEATURES.md) | Current list of missing and partial features, with impact and evidence |
| [review-20261023/IMPLEMENTATION.md](review-20261023/IMPLEMENTATION.md) | Setup/configuration feature implementation, default choices, test results and remaining qualification |
| [KNOWN_TEST_FAILURES.md](KNOWN_TEST_FAILURES.md) | Known test failures and their causes |
| [tasks/](tasks/) | Open task notes and backlogs (incomplete tasks, postponed work, deferred firmware backlog) |

## Archive

[Archived/](Archived/) holds finished task sets, point-in-time reviews, audits and superseded guides. They are
kept for provenance and may describe code that has since changed. `review-20261023/` holds the
page-by-page screenshots of the original Mission Planner used for the 2026-10-23 comparison.
