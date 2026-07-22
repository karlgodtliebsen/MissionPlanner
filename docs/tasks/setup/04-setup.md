# Setup 04 — Accelerometer and level calibration workflows

**Implementation status:** Completed 2026-07-22.

## Objective

Implement guided accelerometer calibration, level calibration, and related orientation workflows using ArduPilot command/progress/status protocols.

## Dependencies

Setup task 01 and command/status-message infrastructure.


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

Create a calibration state machine in Core. Consume command ACKs, `COMMAND_LONG` calibration commands, status text, and orientation/progress signals supported by ArduPilot. UI must be a projection of the state machine.

## Implementation requirements

1. Model NotStarted, Preparing, WaitingForOrientation, Sampling, Completing, Success, Failed, Cancelled, and Disconnected.
2. Implement six-position accelerometer calibration and simple level calibration.
3. Parse explicit calibration progress/ack messages where available; use status text only as supplemental evidence.
4. Present orientation instructions and existing repository images.
5. Prevent competing calibrations/commands.
6. Support cancellation and recovery after disconnect.
7. Refresh calibration-related parameters/status after completion.
8. Keep firmware-specific protocol details behind an ArduPilot calibration service.

## Tests

- Full state-machine transition tests.
- Out-of-order/duplicate progress messages.
- Failure/timeout/cancel/disconnect tests.
- SITL smoke test if SITL supports the workflow deterministically.

## Acceptance criteria

- Guided calibration completes without blocking UI.
- Progress and required orientation are unambiguous.
- Success is based on protocol confirmation, not button completion.
- Reconnect leaves a recoverable state.
