# Flight Data 06 — Servo, relay, and auxiliary function control

## Objective

Complete Servo/Relay and Aux Function tabs with explicit output-state observation, capability-aware command execution, and safeguards against unintended actuator movement.

## Dependencies

Flight Data tasks 01–02 and servo-output/command coverage.


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

Use generated `SERVO_OUTPUT_RAW`, `RC_CHANNELS`, command messages, `VehicleServoOutputObservation`, parameter metadata/values, and command ACK services. Study legacy behavior but redesign around typed services.

## Implementation requirements

1. Display live servo outputs by channel with PWM, freshness, and mapped function from `SERVOx_FUNCTION`.
2. Add controlled servo test/override workflow using `MAV_CMD_DO_SET_SERVO` only when supported and permitted.
3. Add relay state/control using `MAV_CMD_DO_SET_RELAY` and repeat/cycle commands only with explicit bounded parameters.
4. Resolve auxiliary functions from ArduPilot parameters and supported auxiliary-function command mechanisms.
5. Require hold-to-confirm or explicit confirmation for actuator-changing operations; prohibit unsafe operations while armed unless policy allows them.
6. Provide automatic stop/reset on cancellation, disconnect, tab close, or timeout where the protocol permits.
7. Separate observed output from commanded output and show ACK/state mismatch.
8. Add expert warnings for motor-related functions.

## Tests

- Channel/function mapping tests.
- Safety-policy tests for armed state and motor functions.
- ACK/timeout/disconnect/reset tests.
- UI tests proving commands cannot be double-fired.

## Acceptance criteria

- Live output values are visible.
- Servo/relay/aux commands are typed, acknowledged, and safety gated.
- Closing the page cannot leave a repeating test operation running.
- The UI never presents a command request as measured actuator state.
