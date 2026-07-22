# Setup 05 — Compass discovery, orientation, calibration, and priority

**Implementation status:** Completed 2026-07-22.

## Objective

Implement compass setup covering detected devices, use/external flags, orientation, priority, offsets, motor compensation visibility, and guided calibration.

## Dependencies

Setup tasks 01 and 04 patterns.


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

Use compass-related parameters, device IDs, sensor observations, calibration command/progress messages, and metadata. Support multiple compasses without fixed count assumptions.

## Implementation requirements

1. Discover compass instances from parameter presence/device IDs.
2. Display device identity, enabled/external status, orientation, priority, health, and offsets.
3. Implement guided onboard compass calibration with progress per compass, acceptance/cancel/retry.
4. Provide orientation selection from official enum values/metadata.
5. Validate duplicate device IDs and inconsistent priorities.
6. Separate calibration from parameter editing and confirm readback.
7. Warn before disabling the only healthy compass.
8. Add post-calibration quality summary.

## Tests

- Multi-compass discovery and sparse-index tests.
- Calibration progress/state tests.
- Orientation and priority validation tests.
- Safety-policy tests for disable operations.

## Acceptance criteria

- Multiple compasses are handled correctly.
- Calibration status is per device.
- Writes are confirmed and reboot requirements shown.
- Unsafe disable configurations require explicit warning.
