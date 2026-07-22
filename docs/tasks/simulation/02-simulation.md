# Simulation 02 — ArduPilot SITL installation and version management

## Objective

Add safe discovery, acquisition, verification, and selection of ArduPilot SITL binaries for Copter, Plane, Rover, and Sub.

## Dependencies

Simulation task 01.


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

Use official artifact manifests/endpoints through an injectable provider. Support preinstalled binaries and cached downloaded versions. Do not download arbitrary executables from unverified URLs.

## Implementation requirements

1. Detect configured/local SITL installations and query version where possible.
2. Model release channel, firmware family, platform/architecture, version, checksum, source, and install state.
3. Download to versioned cache with progress/cancel and SHA verification.
4. Extract atomically and prevent path traversal.
5. Allow pinning a profile to a version and report missing/incompatible versions.
6. Add cache retention/removal that cannot delete user-selected external installations.
7. Handle Windows/WSL/Linux/macOS differences behind services.
8. Document supported runtime combinations.

## Tests

- Manifest selection/checksum tests.
- Archive path traversal and atomic extraction tests.
- Cache lifecycle tests.
- Platform capability tests.

## Acceptance criteria

- Users can select verified SITL versions.
- Downloads are integrity checked.
- External installations remain untouched.
- Profiles remain reproducible by pinned version.
