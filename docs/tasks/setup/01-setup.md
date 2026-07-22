# Setup 01 — Setup shell, workflow catalog, and completion state

**Implementation status:** Completed 2026-07-22.

## Objective

Replace the placeholder Setup page with a vehicle-aware setup workspace that dynamically exposes relevant setup workflows and tracks completion without duplicating configuration logic.


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

Implement `InitSetupView`/`InitSetupViewModel`, navigation, workflow descriptors, active-vehicle binding, and shared setup operation state. Use firmware family/capabilities/parameter presence to determine visibility.

## Implementation requirements

1. Create setup workflow descriptors: Firmware, Frame, Accelerometer, Compass, Radio, Flight Modes, Battery, ESC, Servo Output, Optional Hardware, Safety, and Summary.
2. Provide left navigation or responsive cards appropriate to desktop/tablet/mobile.
3. Model Available, Unsupported, NotConnected, NotStarted, InProgress, Completed, Warning, and Failed.
4. Persist only local completion evidence; always revalidate against current vehicle state/parameters.
5. Add prerequisites and dependency ordering without forcing users through a rigid wizard.
6. Lazy-create workflow view models and cancel work on navigation/vehicle changes.
7. Add shared confirmation/progress/error patterns.
8. Add a setup summary report and links to relevant Config pages.

## Tests

- Workflow visibility by Copter/Plane/Rover and capability.
- Completion-state invalidation after parameter/firmware change.
- Navigation/reconnect/disposal tests.
- DI resolution tests.

## Acceptance criteria

- Setup is no longer a placeholder.
- Unsupported workflows are hidden or explained.
- Each later setup task plugs into one consistent shell.
- No workflow retains a stale vehicle session.
