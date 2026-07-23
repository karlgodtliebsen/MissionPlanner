# Simulation 05 — Scenario runner and automated missions

## Objective

Implement a deterministic scenario runner for scripted simulation exercises and regression tests without embedding unrestricted code execution.

## Dependencies

Simulation tasks 03–04 and command/mission services.


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

Define a declarative scenario format and executor in Core. Reuse vehicle commands, mission upload, observations, event hub, and simulation controls.

## Implementation requirements

1. Define schema for steps such as wait-for-state, set mode, arm, takeoff, upload mission, start mission, wait condition, inject fault, clear fault, land, and assert telemetry.
2. Add schema versioning, validation, timeouts, cancellation, and variables limited to safe typed values.
3. Execute against a selected simulation session/VehicleId.
4. Record step start/end/result, evidence, and telemetry snapshot.
5. Support pause/resume only at safe step boundaries.
6. Add dry-run validation showing required capabilities.
7. Export machine-readable and readable run reports.
8. Do not execute arbitrary shell/script code from scenario files.

## Tests

- Parser/schema/version tests.
- Executor success/failure/timeout/cancel tests with fake vehicle.
- Wrong-vehicle and capability tests.
- SITL smoke scenario: start, connect, arm/takeoff/land where deterministic.

## Acceptance criteria

- Scenarios run reproducibly and are fully auditable.
- Every wait/assert has an explicit timeout.
- Cancellation leaves vehicle/simulator in a defined state where possible.
- No arbitrary code execution is introduced.

## Completion

Completed 2026-07-23. Core now defines a closed schema-version 1 JSON scenario format with
safe typed Boolean/finite-number/bounded-text variables, explicit per-step timeouts, typed
telemetry conditions, and embedded mission-item data. Unknown members and unsupported
versions fail parsing. No shell, script, expression engine, reflection hook, or arbitrary
command-ID field is available.

The executor supports wait-for-state, set mode, arm, takeoff, mission upload/start,
wait/assert telemetry, bounded documented fault injection/reset, and land. Each run binds
to an exact running session and verified `VehicleId`, checks that identity before every
step, and reuses the existing acknowledged vehicle command, mode catalog, mission transfer,
vehicle registry, and simulation-control services. Dry run reports required modes,
mission services, target identity, and live control capabilities without changing the
vehicle. Arm, takeoff, mission start, and fault steps require explicit run confirmation.

Pause/resume occurs only at safe step boundaries. Timeout and cancellation are distinct;
cancellation resets run-owned faults and attempts landing or a ground disarm after arm/takeoff only when the
original target remains connected. Versioned reports include step timing/result/evidence,
telemetry snapshots, validation evidence, and final state, with JSON and readable-text
exports in the Simulation workspace.

Deterministic tests cover schema/version/unknown fields, timeout requirements, typed
variables, dry run, success across every action family, command failure, wait timeout,
cancellation, safe pause/resume, wrong vehicle, missing capability, report formats,
view-model target binding, and DI. The existing opt-in Copter SITL smoke path additionally
runs a bounded GPS/arm/Guided/takeoff/altitude/land scenario when its binary is configured.
See [SIMULATION.md](../../SIMULATION.md) for schema and safety details.
