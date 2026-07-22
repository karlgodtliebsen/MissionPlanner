# Setup 02 — Firmware identity, board information, and firmware management

**Implementation status:** Completed 2026-07-22.

## Objective

Implement a Setup firmware page that identifies the connected autopilot and board and provides a safe architecture for firmware discovery, download, verification, and flashing.

## Dependencies

Setup task 01 and completed firmware identity work.


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

Use the existing `VehicleFirmwareIdentity`, display formatter, `AUTOPILOT_VERSION`, UID/vendor/product/board version, and capability flags. Firmware flashing must be platform- and bootloader-specific behind abstractions.

## Implementation requirements

1. Display derived vehicle label, firmware family/version/release type, Git hash, board version, vendor/product IDs, UID/UID2, MAVLink version, and capabilities.
2. Add firmware manifest provider models for stable/beta/dev channels, vehicle family, board target, checksum, and release notes.
3. Add local cache and cryptographic hash verification.
4. Define `IFirmwareFlashingService` and platform adapters; do not put serial bootloader logic in the view model.
5. Require disconnect from normal MAVLink before flashing and support reconnect detection afterward.
6. Start with read-only identity plus firmware download if safe; mark flashing unavailable when no adapter exists.
7. Add backup-parameters prompt and post-flash restore guidance.
8. Never infer board target solely from marketing names.

## Tests

- Version/identity formatting tests.
- Manifest selection and checksum tests.
- State-machine tests for download/verify/flash/reconnect and cancellation.
- Unsupported-platform behavior tests.

## Acceptance criteria

- Accurate identity information is visible.
- Firmware packages cannot be flashed before verification.
- Unsupported boards/platforms are blocked clearly.
- No legacy flashing code is copied wholesale into UI code.
