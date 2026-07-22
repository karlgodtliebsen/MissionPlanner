# Setup 07 — Battery monitor, power module, and battery failsafe setup

## Objective

Implement battery monitor configuration with live validation, multiple battery instances, monitor/backend selection, scaling/calibration, capacity, and failsafe thresholds.

## Dependencies

Setup task 01 and battery telemetry/parameter services.


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

Use battery observations, battery parameters/metadata, analog calibration values, and firmware-family-specific failsafe parameters. Support sparse instances.

## Implementation requirements

1. Discover battery monitor instances from parameters and telemetry.
2. Configure monitor type, capacity, voltage/current pins or backend, voltage/current multipliers, and serial/CAN options when present.
3. Add measured-vs-reference calibration workflow for voltage and current.
4. Show live voltage/current/remaining/consumed with freshness.
5. Configure low/critical thresholds and actions through metadata-supported values.
6. Validate threshold ordering, capacity, multipliers, and unsupported combinations.
7. Confirm writes by readback and show reboot requirements.
8. Never present estimated remaining percentage as exact capacity truth.

## Tests

- Instance discovery and parameter alias tests.
- Calibration math tests.
- Threshold/action validation per firmware family.
- Write/readback and partial-failure tests.

## Acceptance criteria

- One or more battery monitors can be configured.
- Live readings support calibration without UI-thread blocking.
- Invalid failsafe combinations cannot be saved.
- Unsupported fields are omitted rather than guessed.
