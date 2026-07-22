# Flight Data 02 — Actions tab and safety-aware vehicle commands

## Objective

Complete the Actions tab with the common in-flight and ground actions appropriate to the connected ArduPilot vehicle, backed by acknowledged MAVLink commands and explicit safety policies.

## Dependencies

Flight Data task 01 and completed MAVLink command/ACK infrastructure.


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

Implement `ActionsTabView` and `ActionsTabViewModel`. Reuse `IVehicleCommandService`, `VehicleCommandPolicy`, generated command definitions, command ACK tracking, firmware family, armed state, mode, landed state, and GPS/home state. Add missing command encoders/services only when required.

## Implementation requirements

1. Inventory the actions exposed by legacy Flight Data and choose a safe initial set: Arm, Disarm, Set Mode, Takeoff, Land, RTL, Loiter/Hold, Reboot autopilot, Set Home Here, and Do Action by command ID where supported.
2. Create typed request methods; do not expose raw float parameters from the UI for standard actions.
3. Gate commands by connection, firmware family, armed/landed state, capability, and required telemetry freshness.
4. Require explicit confirmation for disarm while airborne, reboot, and other hazardous actions.
5. Show pending command, ACK result, timeout, denial reason, and final observed state.
6. Populate mode choices from an ArduPilot mode catalog keyed by firmware family; never hard-code Copter modes for Plane/Rover.
7. Prevent duplicate concurrent commands for the same vehicle.
8. Keep advanced raw-command execution behind a clearly marked expert section with validation.

## Tests

- Policy tests for every gating condition.
- Encoder/ACK tests for each supported action.
- View-model tests for confirmation, cancellation, timeout, rejected ACK, and disconnect.
- SITL smoke tests for benign commands such as mode change, arm/disarm when safe, and RTL/land where deterministic.

## Acceptance criteria

- Actions tab is functional, not placeholder UI.
- Every sent command targets the selected vehicle and reports ACK status.
- Hazardous actions cannot execute accidentally.
- Vehicle-family-specific modes and action availability are correct.
- No UI optimistic state is presented as confirmed before telemetry/ACK confirmation.
