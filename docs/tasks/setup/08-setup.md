# Setup 08 — ESC calibration, motor test, and servo output setup

## Objective

Implement ESC/motor/servo setup with strict armed-state policies and bounded actuator tests.

## Dependencies

Setup task 01 and Flight Data servo/relay foundations where available.


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

Use servo output observations, motor-test commands, ESC telemetry, actuator function parameters, and command ACK services. Separate multirotor motor testing from fixed-wing servo testing.

## Implementation requirements

1. Detect vehicle family and expose only applicable workflows.
2. Implement ESC calibration guidance/status where supported; avoid pretending every ESC protocol requires calibration.
3. Add motor test with motor index, throttle type/value, duration, sequence, and emergency stop.
4. Require disarmed state, explicit propeller-removal warning, hold-to-confirm, and bounded maximums.
5. Add servo output function assignment/readback where suitable.
6. Stop/cancel operations on release, timeout, navigation, or disconnect.
7. Display ESC telemetry and detected protocols where available.
8. Add audit-style operation log for actuator tests.

## Tests

- Safety policy and bound validation tests.
- Emergency-stop/cancel/disconnect tests.
- Vehicle-family visibility tests.
- Command encoding/ACK tests.

## Acceptance criteria

- No actuator test can run unbounded.
- Motor tests are blocked while armed.
- Unsupported ESC calibration paths are explained.
- Every operation has observable start/stop/result.
