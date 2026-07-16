# Documentation Index

The new application lives in `src/`; the original MissionPlanner v1.38 (WinForms) source
is kept for reference in `src-v.1.38/`.

## Core documents (keep these current)

| Document | Purpose |
|---|---|
| [DESIGN_CONCEPTS.md](DESIGN_CONCEPTS.md) | Architecture philosophy: layers, DDD, pipeline, immutable state, UI patterns |
| [ARCHITECTURE_DESICION_RECORDS.md](ARCHITECTURE_DESICION_RECORDS.md) | ADRs, naming conventions, data flow, future direction |
| [FEATURES.md](FEATURES.md) | Feature status per area (domain, UI screens): implemented vs missing |

## Subsystem references

| Document | Purpose |
|---|---|
| [VEHICLE_CONNECTION.md](VEHICLE_CONNECTION.md) | Connection model, transports & resilience, telemetry streams, cleanup, test practices |
| [PARAMETERS.md](PARAMETERS.md) | Parameter request/set, bulk streaming, storage, metadata system |
| [SERILOG_QUICKSTART.md](SERILOG_QUICKSTART.md) | Logging setup and diagnostic workflow |

Historical point-in-time analysis and fix logs (HUD integration analysis, parameter
streaming debugging iterations, connection fix reports, etc.) were consolidated into the
documents above in July 2026 and deleted; recover them from git history if needed.
