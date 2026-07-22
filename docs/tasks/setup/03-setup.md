# Setup 03 — Frame class, frame type, and vehicle-specific initial parameters

**Implementation status:** Completed 2026-07-22.

## Objective

Implement frame selection and initial parameter setup for ArduCopter and corresponding initial-configuration choices for Plane/Rover where applicable.

## Dependencies

Setup task 01 and parameter metadata/value services.


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

Use parameter metadata and live parameter services. Detect available parameters rather than assuming a firmware version. Reference legacy frame visuals read-only, but create new models and UI.

## Implementation requirements

1. Build firmware-family-specific frame option catalogs.
2. For Copter, support `FRAME_CLASS` and `FRAME_TYPE` with metadata-derived values and images where licensing permits existing repository assets.
3. Show current vs pending values and reboot-required indication.
4. Write changes transactionally where possible; report partial failures and provide rollback guidance.
5. Add initial-parameter recommendations as explicit user-reviewed changes, never silent writes.
6. Refresh relevant parameters after write/reboot.
7. Hide unsupported choices based on parameter presence and metadata values.
8. Record setup evidence only after confirmed readback.

## Tests

- Catalog mapping and parameter-presence tests.
- Write/readback/reboot-required tests.
- Partial failure/cancellation tests.
- Family-specific visibility tests.

## Acceptance criteria

- Users can select supported frame configuration safely.
- UI never offers values absent from connected firmware metadata.
- Confirmed values survive reconnect/reload.
- No implicit bulk parameter changes.
