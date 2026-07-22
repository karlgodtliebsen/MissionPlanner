# Setup 06 — Radio input calibration and flight-mode assignment

**Implementation status:** Completed 2026-07-22.

## Objective

Implement radio calibration and flight-mode setup with live channel visualization, endpoint capture, reversal awareness, mode-slot mapping, and failsafe-safe behavior.

## Dependencies

Setup task 01 and RC/mode telemetry promotion.


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

Use RC channel observations, radio-status/failsafe state, parameter metadata, mode catalogs, and parameter write/readback services.

## Implementation requirements

1. Display available RC channels, PWM, normalized value, trim, min/max, reversal, and stale state.
2. Add an explicit calibration workflow with start, movement capture, validation, write, and confirmed readback.
3. Require safe vehicle state and explain transmitter actions.
4. Detect insufficient movement, invalid ranges, duplicate mappings, and throttle reversal hazards.
5. Add flight-mode channel selection and mode-slot editing based on firmware family and parameter presence.
6. Show current active mode slot live.
7. Include simple/super-simple options only where supported.
8. Do not modify transmitter configuration automatically.

## Tests

- Endpoint capture/validation tests.
- Sparse/high channel count tests.
- Mode catalog and parameter mapping tests per family.
- Disconnect and stale-input tests.

## Acceptance criteria

- Radio calibration yields validated, confirmed parameter values.
- Mode choices are correct for the connected firmware family.
- Stale RC telemetry cannot be mistaken for live input.
- Throttle-related hazards are explicitly guarded.
