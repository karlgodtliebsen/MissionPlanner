# Config 02 — GeoFence configuration and fence synchronization

## Objective

Complete Geo Fence configuration for ArduPilot vehicle families, including parameter settings, polygon/circle fence data, upload/download, validation, and map integration.

## Dependencies

Config task 01 and mission/fence protocol services.


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

Use mission/fence protocol services and `MissionMapView` abstractions. Do not treat fence points as ordinary mission waypoints internally. Support capabilities and firmware-family differences.

## Implementation requirements

1. Model fence enable/type/action, altitude/radius/margin parameters, return altitude, and breach options based on available metadata.
2. Implement download/upload/clear of fence geometry through the appropriate MAVLink fence/mission protocol.
3. Add polygon inclusion/exclusion and circle inclusion/exclusion where supported.
4. Validate minimum vertices, closure, self-intersection, radius, altitude ordering, and protocol limits.
5. Display/edit geometry on the map with an explicit fence-edit mode.
6. Show local dirty vs vehicle-synchronized revisions and transfer progress.
7. Require confirmation for clear/replace and preserve a local backup.
8. Test reconnect and partial transfer recovery.

## Tests

- Geometry validation tests.
- Protocol upload/download/clear tests with fake vehicle.
- Parameter plus geometry transaction behavior.
- Map view-model tests and reconnect recovery.

## Acceptance criteria

- Fence parameters and geometry can be round-tripped.
- Invalid geometry cannot be uploaded.
- Vehicle and local versions are distinguishable.
- Clear/replace is recoverable and confirmed.

## Completion

Completed 2026-07-22. GeoFence now uses the shared vehicle-scoped parameter session and a
dedicated fence aggregate/protocol mapper for acknowledged download, upload, and clear.
The Config page supports map editing for polygon/circle inclusion and exclusion plus the
return point, pre-transfer validation, progress, local versus synchronized revisions,
recoverable replace/clear backups, reconnect/partial-transfer recovery, DI registration,
and deterministic domain, protocol, and view-model tests.
