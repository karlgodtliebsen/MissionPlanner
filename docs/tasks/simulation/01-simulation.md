# Simulation 01 — Simulation workspace and session state machine

## Objective

Replace the Simulation placeholder with a workspace that manages simulator profiles, lifecycle, connection endpoints, logs, and active simulation sessions.


## Repository constraints

- Work only under `src/`, `docs/`, `scripts/`, and test-data folders belonging to the new solution.
- Treat `src-v.1.38/` as read-only reference material. Never modify, format, move, or include legacy files in commits.
- Preserve the existing layered architecture: wire protocol in `MissionPlanner.MavLink`, transport in `MissionPlanner.Transport`, application/domain behavior in `MissionPlanner.Core`, and MAUI presentation in `MissionPlanner.App`.
- Do not call MAVLink transports directly from views or code-behind. Use application/domain services injected into view models.
- Keep code-behind limited to view lifecycle and unavoidable platform/UI integration.
- Use CommunityToolkit.Mvvm patterns already present in the solution.
- Reuse `VehicleId`, `VehicleSession`, `VehicleRegistry`, command ACK tracking, parameter services, domain event hub, generated MAVLink messages, and decoder catalog rather than creating parallel abstractions.
- All vehicle-changing operations must be connection-aware, cancellation-aware, target the active `VehicleId`, and expose command acknowledgement or an explicit failure state.
- Add structured logging at workflow boundaries; do not log high-frequency telemetry on every update.
- Add unit tests for domain/application behavior and view-model tests. Add smoke/integration tests only where they are deterministic.
- Update DI registrations in the existing configurators and add DI validation tests.
- The solution must build with nullable warnings treated consistently with the repository.

## Scope

Implement `SimulationView`/`SimulationViewModel` and Core abstractions without binding directly to process APIs. Support local SITL first while leaving remote/container adapters possible.

## Implementation requirements

1. Define simulator profile, vehicle firmware family, frame/model, location, speedup, ports, binary/version, arguments, environment, and persistence.
2. Define session states: Stopped, Validating, Starting, WaitingForHeartbeat, Running, Stopping, Completed, Failed.
3. Add `ISimulatorRuntime` and process/container/remote-neutral session abstractions.
4. Build responsive profile editor, start/stop/restart, live stdout/stderr, elapsed time, PID/runtime identity, and connection endpoint display.
5. Validate port conflicts, paths, executable permissions, and incompatible options before start.
6. Ensure application shutdown and cancellation stop owned processes cleanly.
7. Never kill unrelated processes by name; track exact owned process/session identity.
8. Add structured simulator logs and diagnostics bundle.

## Tests

- State-machine tests.
- Validation and port-conflict tests.
- Fake runtime start/stop/crash/timeout tests.
- View-model navigation and app-shutdown tests.

## Acceptance criteria

- Simulation is no longer a placeholder.
- Sessions are explicit, cancellable, and observable.
- Owned processes are cleaned up safely.
- Runtime adapters can be added without changing the UI model.

## Completion

Completed 2026-07-23. The placeholder is now a persisted profile and live-session
workspace. Core owns process/container/remote-neutral runtime contracts, strict profile and
host validation, all required lifecycle states, bounded stdout/stderr, exact runtime/PID
identity, endpoints, cancellation, crash/timeout handling, and bounded cleanup. Navigation
only detaches presentation observation; application shutdown stops the exact owned session.
Profiles contain typed firmware, model, location, speedup, endpoints, binary/version,
argument tokens, and environment data. Redacted JSON diagnostics and structured lifecycle
logging are available. The default runtime explicitly reports unavailable until the
verified ArduPilot SITL adapter is introduced in sequential task 03. Fake-runtime,
validation, persistence, diagnostics, navigation, shutdown, and DI tests cover this
boundary; [SIMULATION.md](../../SIMULATION.md) documents it.
